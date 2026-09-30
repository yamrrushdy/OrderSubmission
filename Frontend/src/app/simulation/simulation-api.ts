import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

export interface FaultSettings {
  simulateOutage: boolean;
  failFirstAttempts: number;
  failureRate: number;
}

@Injectable({ providedIn: 'root' })
export class SimulationApi {
  private readonly http = inject(HttpClient);
  private readonly url = '/api/simulation/notifications';

  get(): Observable<FaultSettings> {
    return this.http.get<FaultSettings>(this.url);
  }

  update(settings: FaultSettings): Observable<FaultSettings> {
    return this.http.put<FaultSettings>(this.url, settings);
  }
}
