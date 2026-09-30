import { Component, inject, signal } from '@angular/core';

import { FaultSettings, SimulationApi } from './simulation-api';

const MAX_FAILURES = 5;

@Component({
  selector: 'app-delivery-simulator',
  templateUrl: './delivery-simulator.html',
  styleUrl: './delivery-simulator.scss',
})
export class DeliverySimulator {
  private readonly api = inject(SimulationApi);

  protected readonly settings = signal<FaultSettings | null>(null);
  protected readonly error = signal<string | null>(null);
  protected readonly maxFailures = MAX_FAILURES;
  protected readonly rates = [0, 0.25, 0.5];

  constructor() {
    this.api.get().subscribe({
      next: (settings) => this.settings.set(settings),
      error: () => this.error.set('Simulation settings are unavailable.'),
    });
  }

  protected setOutage(simulateOutage: boolean): void {
    this.apply({ simulateOutage });
  }

  protected changeFailures(delta: number): void {
    const current = this.settings()?.failFirstAttempts ?? 0;
    this.apply({ failFirstAttempts: Math.min(MAX_FAILURES, Math.max(0, current + delta)) });
  }

  protected setRate(failureRate: number): void {
    this.apply({ failureRate });
  }

  private apply(change: Partial<FaultSettings>): void {
    const previous = this.settings();
    if (!previous) return;

    const next = { ...previous, ...change };
    this.settings.set(next);
    this.error.set(null);

    this.api.update(next).subscribe({
      next: (saved) => this.settings.set(saved),
      error: () => {
        this.settings.set(previous);
        this.error.set('Could not update the simulation. Try again.');
      },
    });
  }
}
