export type NotificationStatus = 'Pending' | 'Failed' | 'Delivered';

export interface OrderLine {
  lineNumber: number;
  productCode: string;
  quantity: number;
  unitPrice: number;
  lineTotal: number;
}

export interface NotificationState {
  status: NotificationStatus;
  attempts: number;
  lastAttemptAt: string | null;
  nextAttemptAt: string | null;
  deliveredAt: string | null;
  lastError: string | null;
}

export interface Order {
  id: string;
  customerReference: string;
  total: number;
  placedAt: string;
  lines: OrderLine[];
  notification: NotificationState;
}

export interface OrderLineInput {
  productCode: string;
  quantity: number;
  unitPrice: number;
}

export interface SubmitOrderRequest {
  customerReference: string;
  lines: OrderLineInput[];
}

export interface ProblemDetails {
  title?: string;
  detail?: string;
  status?: number;
  code?: string;
  errors?: Record<string, string[]>;
}
