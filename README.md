# Reliable Order Submission

Order submission service that stays correct when clients retry and when the notification provider is down.

- **Backend:** ASP.NET Core 10 (minimal APIs), EF Core 10, SQL Server
- **Frontend:** Angular 22 (standalone components, signals, reactive forms)
- **Tests:** xUnit — domain/unit tests plus API-level integration tests running against a real SQL Server database

## Running it

Prerequisites: .NET 10 SDK, Node 22+, a local SQL Server instance (Windows auth by default).

```bash
# 1. Build the UI into the API's wwwroot (once, or after UI changes)
cd client
npm ci
npm run build

# 2. Run the API. It applies migrations on startup and serves the UI.
cd ..
dotnet run --project src/OrderSubmission.Api --launch-profile http
```

Open http://localhost:5159.

The connection string lives in `src/OrderSubmission.Api/appsettings.json` (`ConnectionStrings:Orders`). It points to `Server=.` and creates an `OrderSubmission` database.

For UI development, run `npm start` in `client/` instead of building; the dev server proxies `/api` to the API on port 5159.

### Tests

```bash
dotnet test
```

Integration tests create a throwaway database per run (`OrderSubmission_Tests_<guid>`) and drop it at the end. They use `Server=.` with Windows auth. To use another server, set `ORDERS_TEST_SQLSERVER`, for example `Server=localhost,1433;User Id=sa;Password=...;TrustServerCertificate=True`.

## Solution layout

```
src/
  OrderSubmission.Domain          Order aggregate, value objects, OrderNotification, Result/Error types
  OrderSubmission.Application     Use cases (submit, get, dispatch), idempotency, ports (repositories, sender)
  OrderSubmission.Infrastructure  EF Core + SQL Server, repositories, fake sender, background worker
  OrderSubmission.Api             HTTP endpoints, error-to-ProblemDetails mapping, hosts the SPA
tests/
  OrderSubmission.UnitTests
  OrderSubmission.IntegrationTests
client/                           Angular app
```

Dependencies point inwards (Api → Infrastructure → Application → Domain). The domain has no framework references. Business failures are returned as `Result`/`Error` values instead of exceptions, and the API maps them to RFC 7807 problem responses (`400` validation with per-field errors, `404`, `409`).

## How it works

### Idempotent submission

`POST /api/orders` requires an `Idempotency-Key` header (1–100 printable ASCII characters).

1. The request is reduced to a canonical form: trimmed customer reference, upper-cased product codes, and normalised prices, so `10` and `10.00` are the same. The canonical form is hashed with SHA-256.
2. If the key already exists:
   - **same hash:** the stored order is returned with `200 OK` and `Idempotent-Replayed: true`.
   - **different hash:** `409 Conflict` (`idempotency.key_reused`).
3. Otherwise the order, its lines, the pending notification and the idempotency record are inserted in **one `SaveChanges`, which means one transaction**.

**Concurrency** is handled by the database, not by in-process locks, so it also holds across multiple instances. `IdempotencyKeys.Key` is the primary key. When two requests race, one commits and the other hits a unique-key violation. That violation is turned into a `DuplicateKeyException`, the change tracker is cleared, and the loser re-reads the winner's record and goes through step 2. It then returns the same order, or `409` if its payload was different. A new order returns `201 Created` with a `Location` header.

Requests that fail validation are not recorded. The client can fix the payload and reuse the same key.

### Notifications (transactional outbox)

Every order gets an `OrderNotification` row written in the same transaction, so there is never an order without a notification, or the other way round.

`NotificationWorker` (a `BackgroundService`) polls every 2s and asks `NotificationDispatcher` to process due rows:

- **Claiming** is one atomic statement: `UPDATE TOP(n) … WITH (UPDLOCK, READPAST) … OUTPUT inserted.*`. It pushes `NextAttemptAt` forward by a lease (30s). Parallel workers or instances skip rows that are already claimed, and a crashed worker's rows come back when the lease expires.
- **Success:** `Delivered`, with the attempt count and timestamp recorded.
- **Failure:** `Failed`, with the attempt count, error message and `NextAttemptAt` recorded, using exponential backoff (5s, 10s, 20s … capped at 5 min). A `Failed` notification is still due for retry. Nothing is ever dead-lettered, so failed delivery always stays retryable.
- Each notification is saved right after its own attempt, so one bad row cannot roll back the others.

`GET /api/orders/{id}` returns the order together with `notification.status`, `attempts`, `lastError`, `nextAttemptAt` and `deliveredAt`.

### Simulating failures

The fake provider can be made to fail in three ways:

| Setting | Effect |
|---|---|
| `SimulateOutage` | every attempt fails |
| `FailFirstAttempts` | each notification fails its first N attempts, then succeeds |
| `FailureRate` | each attempt fails with probability 0–1 |

The initial values come from `Notifications:Fake` in `appsettings.json`. They can be changed at runtime through `PUT /api/simulation/notifications` or the **Delivery simulation** card in the UI.

### Database

Schema `ordering`:

| Table | Notes |
|---|---|
| `Orders` | `Total decimal(19,2)`, stored server-side and checked `> 0` |
| `OrderLines` | PK `(OrderId, LineNumber)` keeps lines physically together with their order. Check constraints on quantity and price. |
| `OrderNotifications` | status is a `varchar` with a check constraint. A further check ties `Status`/`DeliveredAt`/`NextAttemptAt` together. The filtered index `IX_OrderNotifications_Due` covers only undelivered rows, so the worker's query stays cheap as history grows. |
| `IdempotencyKeys` | PK on key, `RequestHash binary(32)`, unique FK to the order |

GUID primary keys are non-clustered. Each table is clustered on its creation timestamp, which avoids page splits from random GUID inserts and suits time-range queries and retention jobs.

### UI

- A new idempotency key is generated when a payload is submitted for the first time. Submitting the **same** payload again, or pressing **Retry**, reuses that key. Changing the order starts a new key.
- Network errors and 5xx responses are retried automatically, twice, with the same key. After that, the user gets a manual **Retry submission** button.
- The pending submission (key and payload) is saved in `sessionStorage`, so reloading the page mid-failure still retries with the original key.
- After success the panel polls the order until the notification is delivered. It shows failed attempts, the last error and a countdown to the next retry.
- Server validation errors are mapped back onto the matching form fields (`lines[1].quantity` → the second line's quantity input).

## Assumptions

- Single currency. Prices have at most 2 decimal places. Quantities are whole numbers, 1–10,000. Unit price is capped at 1,000,000. An order has at most 100 lines.
- The customer reference is an opaque identifier of up to 64 characters. There is no customer lookup.
- Line order is part of the request identity, and duplicate product codes are kept as separate lines rather than merged.
- Idempotency keys are global, not scoped per client, and are kept indefinitely. In production they would be scoped to the authenticated caller and expired after a retention window, for example 24–72h.
- Notification delivery is at-least-once. The notification id is passed to the provider so that a real provider could de-duplicate.
- A replayed response reflects the order's **current** notification status, not a byte-for-byte copy of the first response.
- The simulation endpoint has no authentication, because it only controls the fake provider. It would not exist in a real deployment.

## Not done / next steps

- Authentication, and scoping idempotency keys per caller.
- A cleanup job for expired idempotency keys and old delivered notifications.
- Health checks, OpenTelemetry metrics (queue depth, delivery latency, retry counts) and alerting on notifications stuck in `Failed`.
- Publishing the UI as part of `dotnet publish` (an MSBuild target or a Dockerfile), and a CI pipeline running the tests against a SQL Server container.
- Front-end unit tests.
