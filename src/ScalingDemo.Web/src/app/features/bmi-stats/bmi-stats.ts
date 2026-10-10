import { DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { BmiStore } from '../../core/bmi-store';
import { CATEGORY_META, CATEGORY_ORDER, STATS_KEY } from '../../shared/bmi-category';

@Component({
  selector: 'app-bmi-stats',
  imports: [DecimalPipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './bmi-stats.html',
})
export class BmiStats {
  protected readonly store = inject(BmiStore);
  protected readonly meta = CATEGORY_META;

  protected readonly rows = computed(() => {
    const stats = this.store.stats();
    if (!stats) {
      return [];
    }

    return CATEGORY_ORDER.map((category) => {
      const count = stats[STATS_KEY[category]];
      return { category, count, percent: stats.total ? (count / stats.total) * 100 : 0 };
    });
  });
}
