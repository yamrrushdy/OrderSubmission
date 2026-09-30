import { HttpErrorResponse } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import {
  EMPTY,
  Subscription,
  catchError,
  defer,
  retry,
  switchMap,
  takeWhile,
  throwError,
  timer,
} from 'rxjs';

import { Order, ProblemDetails, SubmitOrderRequest } from './order.models';
import { OrdersApi, REPLAYED_HEADER } from './orders-api';

export type SubmissionPhase = 'idle' | 'submitting' | 'succeeded' | 'failed';

export type FailureKind = 'unreachable' | 'server' | 'conflict' | 'validation';

export interface SubmissionFailure {
  kind: FailureKind;
  message: string;
  fieldErrors: Record<string, string[]>;
}

export interface SubmissionAttempt {
  key: string;
  payload: SubmitOrderRequest;
  fingerprint: string;
}

const STORAGE_KEY = 'orders.pending-submission';
const AUTOMATIC_RETRIES = 2;
const POLL_INTERVAL_MS = 1500;

@Injectable({ providedIn: 'root' })
export class SubmissionStore {
  private readonly api = inject(OrdersApi);

  private request?: Subscription;
  private polling?: Subscription;

  private readonly restored = readPending();

  readonly phase = signal<SubmissionPhase>(this.restored ? 'failed' : 'idle');
  readonly attempt = signal<SubmissionAttempt | null>(this.restored);
  readonly sends = signal(0);
  readonly order = signal<Order | null>(null);
  readonly replayed = signal(false);
  readonly failure = signal<SubmissionFailure | null>(
    this.restored
      ? {
          kind: 'unreachable',
          message: 'Your last submission was not confirmed before the page was closed.',
          fieldErrors: {},
        }
      : null,
  );

  readonly busy = computed(() => this.phase() === 'submitting');
  readonly retryable = computed(() => {
    const failure = this.failure();
    return (
      this.phase() === 'failed' && (failure?.kind === 'unreachable' || failure?.kind === 'server')
    );
  });

  submit(payload: SubmitOrderRequest): void {
    const fingerprint = JSON.stringify(payload);
    const current = this.attempt();
    const sameSubmission = current?.fingerprint === fingerprint;

    const attempt = sameSubmission ? current : { key: crypto.randomUUID(), payload, fingerprint };

    if (!sameSubmission) {
      this.sends.set(0);
    }

    this.send(attempt);
  }

  retry(): void {
    const attempt = this.attempt();
    if (attempt) {
      this.send(attempt);
    }
  }

  reset(): void {
    this.request?.unsubscribe();
    this.polling?.unsubscribe();
    clearPending();

    this.phase.set('idle');
    this.attempt.set(null);
    this.sends.set(0);
    this.order.set(null);
    this.replayed.set(false);
    this.failure.set(null);
  }

  private send(attempt: SubmissionAttempt): void {
    this.request?.unsubscribe();
    this.polling?.unsubscribe();

    this.attempt.set(attempt);
    this.phase.set('submitting');
    this.failure.set(null);
    writePending(attempt);

    this.request = defer(() => {
      this.sends.update((count) => count + 1);
      return this.api.submit(attempt.payload, attempt.key);
    })
      .pipe(
        retry({
          count: AUTOMATIC_RETRIES,
          delay: (error: unknown, retryCount: number) =>
            isTransient(error) ? timer(500 * 2 ** retryCount) : throwError(() => error),
        }),
      )
      .subscribe({
        next: (response) => {
          clearPending();
          this.order.set(response.body);
          this.replayed.set(response.headers.get(REPLAYED_HEADER) === 'true');
          this.phase.set('succeeded');

          if (response.body) {
            this.watchDelivery(response.body);
          }
        },
        error: (error: unknown) => {
          const failure = describeFailure(error);
          if (failure.kind === 'conflict' || failure.kind === 'validation') {
            clearPending();
          }

          this.failure.set(failure);
          this.phase.set('failed');
        },
      });
  }

  private watchDelivery(order: Order): void {
    if (order.notification.status === 'Delivered') {
      return;
    }

    this.polling = timer(POLL_INTERVAL_MS, POLL_INTERVAL_MS)
      .pipe(
        switchMap(() => this.api.get(order.id).pipe(catchError(() => EMPTY))),
        takeWhile((latest) => latest.notification.status !== 'Delivered', true),
      )
      .subscribe((latest) => this.order.set(latest));
  }
}

function isTransient(error: unknown): boolean {
  return error instanceof HttpErrorResponse && (error.status === 0 || error.status >= 502);
}

function describeFailure(error: unknown): SubmissionFailure {
  if (!(error instanceof HttpErrorResponse)) {
    return {
      kind: 'server',
      message: 'Something went wrong while submitting the order.',
      fieldErrors: {},
    };
  }

  const problem = (error.error ?? {}) as ProblemDetails;

  if (error.status === 0) {
    return {
      kind: 'unreachable',
      message: 'The server could not be reached. The order may or may not have been received.',
      fieldErrors: {},
    };
  }

  if (error.status === 409) {
    return {
      kind: 'conflict',
      message: problem.detail ?? 'This submission conflicts with an earlier one.',
      fieldErrors: {},
    };
  }

  if (error.status === 400) {
    return {
      kind: 'validation',
      message: problem.detail ?? 'Please fix the highlighted fields.',
      fieldErrors: problem.errors ?? {},
    };
  }

  return {
    kind: 'server',
    message: problem.detail ?? `The server responded with ${error.status}. It is safe to retry.`,
    fieldErrors: {},
  };
}

function readPending(): SubmissionAttempt | null {
  try {
    const raw = sessionStorage.getItem(STORAGE_KEY);
    return raw ? (JSON.parse(raw) as SubmissionAttempt) : null;
  } catch {
    return null;
  }
}

function writePending(attempt: SubmissionAttempt): void {
  try {
    sessionStorage.setItem(STORAGE_KEY, JSON.stringify(attempt));
  } catch {
    return;
  }
}

function clearPending(): void {
  try {
    sessionStorage.removeItem(STORAGE_KEY);
  } catch {
    return;
  }
}
