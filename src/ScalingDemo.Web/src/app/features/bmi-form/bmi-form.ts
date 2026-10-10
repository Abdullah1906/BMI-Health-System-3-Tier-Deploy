import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { BmiStore } from '../../core/bmi-store';

type NumericField = 'age' | 'heightCm' | 'weightKg';
type Field = NumericField | 'name';

const LIMITS: Record<NumericField, { min: number; max: number; unit: string }> = {
  age: { min: 18, max: 120, unit: 'years' },
  heightCm: { min: 50, max: 260, unit: 'cm' },
  weightKg: { min: 10, max: 500, unit: 'kg' },
};

@Component({
  selector: 'app-bmi-form',
  imports: [ReactiveFormsModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './bmi-form.html',
})
export class BmiForm {
  private readonly fb = inject(NonNullableFormBuilder);
  protected readonly store = inject(BmiStore);

  protected readonly form = this.fb.group({
    name: this.fb.control('', [Validators.maxLength(100)]),
    age: this.fb.control<number | null>(null, [
      Validators.required,
      Validators.min(LIMITS.age.min),
      Validators.max(LIMITS.age.max),
    ]),
    heightCm: this.fb.control<number | null>(null, [
      Validators.required,
      Validators.min(LIMITS.heightCm.min),
      Validators.max(LIMITS.heightCm.max),
    ]),
    weightKg: this.fb.control<number | null>(null, [
      Validators.required,
      Validators.min(LIMITS.weightKg.min),
      Validators.max(LIMITS.weightKg.max),
    ]),
  });

  protected submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const { name, age, heightCm, weightKg } = this.form.getRawValue();
    this.store.calculate({
      name: name.trim() || null,
      age: age!,
      heightCm: heightCm!,
      weightKg: weightKg!,
    });
  }

  protected reset(): void {
    this.form.reset();
  }

  /** First validation message for a field, or null while it is untouched or valid. */
  protected errorOf(field: Field): string | null {
    const control = this.form.controls[field];
    const errors = control.touched ? control.errors : null;
    if (!errors) {
      return null;
    }
    if (errors['required']) {
      return 'This field is required.';
    }
    if (errors['maxlength']) {
      return 'Please keep this under 100 characters.';
    }
    if (field !== 'name' && (errors['min'] || errors['max'])) {
      const { min, max, unit } = LIMITS[field];
      return `Enter a value between ${min} and ${max} ${unit}.`;
    }
    return 'This value is not valid.';
  }
}
