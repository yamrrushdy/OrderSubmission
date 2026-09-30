import { DecimalPipe } from '@angular/common';
import { Component, computed, effect, inject, untracked } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import {
  AbstractControl,
  FormArray,
  FormControl,
  FormGroup,
  NonNullableFormBuilder,
  ReactiveFormsModule,
  ValidationErrors,
  Validators,
} from '@angular/forms';

import { OrderLineInput, SubmitOrderRequest } from '../order.models';
import { SubmissionStore } from '../submission-store';

type LineForm = FormGroup<{
  productCode: FormControl<string>;
  quantity: FormControl<number | null>;
  unitPrice: FormControl<number | null>;
}>;

const PRODUCT_CODE = /^[A-Za-z0-9][A-Za-z0-9._-]*$/;
const MAX_LINES = 100;

function twoDecimals(control: AbstractControl<number | null>): ValidationErrors | null {
  const value = control.value;
  return value == null || Math.round(value * 100) / 100 === value ? null : { decimals: true };
}

function wholeNumber(control: AbstractControl<number | null>): ValidationErrors | null {
  const value = control.value;
  return value == null || Number.isInteger(value) ? null : { integer: true };
}

@Component({
  selector: 'app-order-form',
  imports: [ReactiveFormsModule, DecimalPipe],
  templateUrl: './order-form.html',
  styleUrl: './order-form.scss',
})
export class OrderForm {
  protected readonly store = inject(SubmissionStore);
  private readonly fb = inject(NonNullableFormBuilder);

  protected readonly maxLines = MAX_LINES;

  protected readonly form = this.fb.group({
    customerReference: this.fb.control('', [Validators.required, Validators.maxLength(64)]),
    lines: this.fb.array<LineForm>([this.createLine()]),
  });

  private readonly value = toSignal(this.form.valueChanges, {
    initialValue: this.form.getRawValue(),
  });

  protected readonly estimatedTotal = computed(() =>
    (this.value().lines ?? []).reduce(
      (sum, line) => sum + (Number(line?.quantity) || 0) * (Number(line?.unitPrice) || 0),
      0,
    ),
  );

  constructor() {
    const pending = this.store.attempt();
    if (pending) {
      this.load(pending.payload);
    }

    effect(() => {
      const failure = this.store.failure();
      if (failure?.kind === 'validation') {
        untracked(() => this.applyServerErrors(failure.fieldErrors));
      }
    });

    effect(() => {
      if (this.store.attempt() === null) {
        untracked(() => this.clear());
      }
    });
  }

  protected get lines(): FormArray<LineForm> {
    return this.form.controls.lines;
  }

  protected addLine(): void {
    if (this.lines.length < MAX_LINES) {
      this.lines.push(this.createLine());
    }
  }

  protected removeLine(index: number): void {
    if (this.lines.length > 1) {
      this.lines.removeAt(index);
    }
  }

  protected submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.store.submit(this.toRequest());
  }

  protected startOver(): void {
    this.store.reset();
    this.clear();
  }

  protected showError(control: AbstractControl): boolean {
    return control.invalid && (control.touched || control.hasError('server'));
  }

  protected errorFor(control: AbstractControl, label: string): string {
    const errors = control.errors ?? {};

    if (errors['server']) return errors['server'];
    if (errors['required']) return `${label} is required`;
    if (errors['maxlength']) return `Max ${errors['maxlength'].requiredLength} characters`;
    if (errors['pattern']) return 'Letters, digits, . - _ only';
    if (errors['min']) return `Must be at least ${errors['min'].min}`;
    if (errors['max']) return `Must be at most ${errors['max'].max}`;
    if (errors['integer']) return 'Whole numbers only';
    if (errors['decimals']) return 'Max 2 decimal places';

    return 'Invalid value';
  }

  private createLine(line?: OrderLineInput): LineForm {
    return this.fb.group({
      productCode: this.fb.control(line?.productCode ?? '', [
        Validators.required,
        Validators.maxLength(32),
        Validators.pattern(PRODUCT_CODE),
      ]),
      quantity: new FormControl<number | null>(line?.quantity ?? 1, [
        Validators.required,
        Validators.min(1),
        Validators.max(10000),
        wholeNumber,
      ]),
      unitPrice: new FormControl<number | null>(line?.unitPrice ?? null, [
        Validators.required,
        Validators.min(0.01),
        Validators.max(1000000),
        twoDecimals,
      ]),
    });
  }

  private clear(): void {
    this.form.controls.customerReference.reset();
    this.lines.clear();
    this.lines.push(this.createLine());
    this.form.markAsUntouched();
  }

  private load(request: SubmitOrderRequest): void {
    this.lines.clear();
    request.lines.forEach((line) => this.lines.push(this.createLine(line)));
    this.form.controls.customerReference.setValue(request.customerReference);
  }

  private toRequest(): SubmitOrderRequest {
    const value = this.form.getRawValue();

    return {
      customerReference: value.customerReference.trim(),
      lines: value.lines.map((line) => ({
        productCode: line.productCode.trim().toUpperCase(),
        quantity: Number(line.quantity),
        unitPrice: Number(line.unitPrice),
      })),
    };
  }

  private applyServerErrors(fieldErrors: Record<string, string[]>): void {
    for (const [field, messages] of Object.entries(fieldErrors)) {
      const control = this.resolveControl(field);
      if (control) {
        control.setErrors({ ...control.errors, server: messages[0] });
        control.markAsTouched();
      }
    }
  }

  private resolveControl(field: string): AbstractControl | null {
    const path = field
      .replace(/\[(\d+)\]/g, '.$1')
      .split('.')
      .filter(Boolean);

    return path.length === 0 ? null : this.form.get(path);
  }
}
