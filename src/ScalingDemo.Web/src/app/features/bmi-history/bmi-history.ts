import { DatePipe, DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { BmiStore } from '../../core/bmi-store';
import { CATEGORY_META } from '../../shared/bmi-category';

@Component({
  selector: 'app-bmi-history',
  imports: [DatePipe, DecimalPipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './bmi-history.html',
})
export class BmiHistory {
  protected readonly store = inject(BmiStore);
  protected readonly meta = CATEGORY_META;

  protected previous(): void {
    this.store.loadHistory(this.store.page() - 1);
  }

  protected next(): void {
    this.store.loadHistory(this.store.page() + 1);
  }

  protected remove(id: number): void {
    this.store.remove(id);
  }
}
