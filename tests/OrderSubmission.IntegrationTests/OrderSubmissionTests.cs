using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OrderSubmission.Api.Contracts;
using OrderSubmission.Api.Endpoints;
using OrderSubmission.Domain.Notifications;
using OrderSubmission.Domain.Orders;
using OrderSubmission.IntegrationTests.Infrastructure;

namespace OrderSubmission.IntegrationTests;

public sealed class OrderSubmissionTests(OrderingApiFactory factory) : ApiTestBase(factory)
{
    [Fact]
    public async Task New_submission_creates_order_with_server_calculated_total_and_pending_notification()
    {
        var response = await SubmitAsync(SampleOrder(), NewKey());

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var order = await ReadOrderAsync(response);
        Assert.Equal(45.48m, order.Total);
        Assert.Equal(NotificationStatus.Pending, order.Notification.Status);
        Assert.Equal($"/api/orders/{order.Id}", response.Headers.Location?.AbsolutePath);

        var stored = await GetOrderAsync(order.Id);
        Assert.Equal(order.Total, stored.Total);
        Assert.Equal(2, stored.Lines.Count);
        Assert.Equal(NotificationStatus.Pending, stored.Notification.Status);
    }

    [Fact]
    public async Task Duplicate_submission_returns_the_original_order_without_creating_another()
    {
        var key = NewKey();
        var request = SampleOrder();

        var first = await SubmitAsync(request, key);
        var second = await SubmitAsync(request, key);

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.Equal("true", second.Headers.GetValues(OrderEndpoints.ReplayedHeader).Single());

        var original = await ReadOrderAsync(first);
        var replayed = await ReadOrderAsync(second);
        Assert.Equal(original.Id, replayed.Id);
        Assert.Equal(original.Total, replayed.Total);

        Assert.Equal(1, await CountOrdersAsync(request.CustomerReference!));
    }

    [Fact]
    public async Task Duplicate_submission_is_matched_on_meaning_not_formatting()
    {
        var key = NewKey();
        var customer = NewCustomer();
        var request = new SubmitOrderRequest(customer, [new SubmitOrderLineRequest("SKU-1", 1, 10m)]);
        var reformatted = new SubmitOrderRequest($"  {customer} ", [new SubmitOrderLineRequest("sku-1", 1, 10.00m)]);

        var first = await SubmitAsync(request, key);
        var second = await SubmitAsync(reformatted, key);

        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.Equal((await ReadOrderAsync(first)).Id, (await ReadOrderAsync(second)).Id);
    }

    [Fact]
    public async Task Reusing_a_key_with_a_different_payload_returns_conflict()
    {
        var key = NewKey();
        var request = SampleOrder();
        var altered = request with { Lines = [new SubmitOrderLineRequest("SKU-100", 3, 19.99m)] };

        var first = await SubmitAsync(request, key);
        var conflict = await SubmitAsync(altered, key);

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);

        var problem = await conflict.Content.ReadFromJsonAsync<ProblemDetails>(Json);
        Assert.Equal("idempotency.key_reused", problem!.Extensions["code"]?.ToString());

        Assert.Equal(1, await CountOrdersAsync(request.CustomerReference!));
    }

    [Fact]
    public async Task Concurrent_duplicates_create_exactly_one_order()
    {
        const int parallelRequests = 24;
        var key = NewKey();
        var request = SampleOrder();

        using var gate = new SemaphoreSlim(0);
        var submissions = Enumerable.Range(0, parallelRequests)
            .Select(async _ =>
            {
                await gate.WaitAsync();
                return await SubmitAsync(request, key);
            })
            .ToList();

        gate.Release(parallelRequests);
        var responses = await Task.WhenAll(submissions);

        Assert.All(responses, response => Assert.True(response.IsSuccessStatusCode, $"Unexpected {response.StatusCode}"));
        Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Created);

        var orderIds = await Task.WhenAll(responses.Select(async response => (await ReadOrderAsync(response)).Id));
        Assert.Single(orderIds.Distinct());

        Assert.Equal(1, await CountOrdersAsync(request.CustomerReference!));
        Assert.Equal(1, await Factory.QueryAsync(db => db.Notifications.CountAsync(n => n.OrderId == new OrderId(orderIds[0]))));
    }

    [Fact]
    public async Task Concurrent_requests_with_same_key_but_different_payloads_create_one_order_and_conflict_the_rest()
    {
        var key = NewKey();
        var customer = NewCustomer();

        var responses = await Task.WhenAll(Enumerable.Range(1, 10).Select(quantity =>
            SubmitAsync(new SubmitOrderRequest(customer, [new SubmitOrderLineRequest("SKU-1", quantity, 1m)]), key)));

        Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Created);
        Assert.Equal(9, responses.Count(response => response.StatusCode == HttpStatusCode.Conflict));
        Assert.Equal(1, await CountOrdersAsync(customer));
    }

    [Fact]
    public async Task Missing_idempotency_key_is_rejected()
    {
        var response = await SubmitAsync(SampleOrder(), idempotencyKey: null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Invalid_order_returns_field_level_validation_errors_and_does_not_consume_the_key()
    {
        var key = NewKey();
        var customer = NewCustomer();
        var invalid = new SubmitOrderRequest(customer, [new SubmitOrderLineRequest("SKU-1", 0, -1m)]);

        var rejected = await SubmitAsync(invalid, key);

        Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);
        var problem = await rejected.Content.ReadFromJsonAsync<ValidationProblemDetails>(Json);
        Assert.Contains("lines[0].quantity", problem!.Errors.Keys);
        Assert.Contains("lines[0].unitPrice", problem.Errors.Keys);

        var corrected = await SubmitAsync(new SubmitOrderRequest(customer, [new SubmitOrderLineRequest("SKU-1", 1, 1m)]), key);
        Assert.Equal(HttpStatusCode.Created, corrected.StatusCode);
    }

    [Fact]
    public async Task Unknown_order_returns_not_found()
    {
        var response = await Client.GetAsync($"/api/orders/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private Task<int> CountOrdersAsync(string customer)
    {
        var reference = CustomerReference.Create(customer).Value;
        return Factory.QueryAsync(db => db.Orders.CountAsync(order => order.CustomerReference == reference));
    }
}
