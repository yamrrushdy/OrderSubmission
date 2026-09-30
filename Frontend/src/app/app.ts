import { Component } from '@angular/core';

import { OrderForm } from './orders/order-form/order-form';
import { SubmissionPanel } from './orders/submission-panel/submission-panel';
import { DeliverySimulator } from './simulation/delivery-simulator';

@Component({
  selector: 'app-root',
  imports: [OrderForm, SubmissionPanel, DeliverySimulator],
  templateUrl: './app.html',
  styleUrl: './app.scss',
})
export class App {}
