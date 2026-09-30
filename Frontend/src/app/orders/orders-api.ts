import { HttpClient, HttpResponse } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import { Order, SubmitOrderRequest } from './order.models';

export const IDEMPOTENCY_KEY_HEADER = 'Idempotency-Key';
export const REPLAYED_HEADER = 'Idempotent-Replayed';

@Injectable({ providedIn: 'root' })
export class OrdersApi {
  private readonly http = inject(HttpClient);

  submit(request: SubmitOrderRequest, idempotencyKey: string): Observable<HttpResponse<Order>> {
    return this.http.post<Order>('/api/orders', request, {
      headers: { [IDEMPOTENCY_KEY_HEADER]: idempotencyKey },
      observe: 'response',
    });
  }

  get(id: string): Observable<Order> {
    return this.http.get<Order>(`/api/orders/${id}`);
  }
}
