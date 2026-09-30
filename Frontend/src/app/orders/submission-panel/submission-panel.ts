import { DatePipe, DecimalPipe } from '@angular/common';
import { Component, DestroyRef, computed, inject, signal } from '@angular/core';

import { SubmissionStore } from '../submission-store';

type DeliveryStep = 'waiting' | 'retrying' | 'delivered';

@Component({
  selector: 'app-submission-panel',
  imports: [DatePipe, DecimalPipe],
  templateUrl: './submission-panel.html',
  styleUrl: './submission-panel.scss',
})
export class SubmissionPanel {
  protected readonly store = inject(SubmissionStore);

  private readonly now = signal(Date.now());
  protected readonly copied = signal<string | null>(null);

  protected readonly key = computed(() => this.store.attempt()?.key ?? null);
  protected readonly order = this.store.order;
  protected readonly notification = computed(() => this.order()?.notification ?? null);

  protected readonly deliveryStep = computed<DeliveryStep>(() => {
    const notification = this.notification();
    if (notification?.status === 'Delivered') return 'delivered';
    if (notification?.status === 'Failed') return 'retrying';
    return 'waiting';
  });

  protected readonly retryInSeconds = computed(() => {
    const next = this.notification()?.nextAttemptAt;
    if (!next) return null;
    return Math.max(0, Math.ceil((Date.parse(next) - this.now()) / 1000));
  });

  protected readonly failureTitle = computed(() => {
    switch (this.store.failure()?.kind) {
      case 'unreachable':
        return 'Not confirmed';
      case 'conflict':
        return 'Key already used';
      case 'validation':
        return 'Order was rejected';
      default:
        return 'Submission failed';
    }
  });

  constructor() {
    const ticker = setInterval(() => this.now.set(Date.now()), 1000);
    inject(DestroyRef).onDestroy(() => clearInterval(ticker));
  }

  protected async copy(value: string): Promise<void> {
    try {
      await navigator.clipboard.writeText(value);
      this.copied.set(value);
      setTimeout(() => this.copied.set(null), 1500);
    } catch {
      this.copied.set(null);
    }
  }
}
