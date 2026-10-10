import { DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { BmiResult as BmiResultModel } from '../../core/models';
import { CATEGORY_META, GAUGE, gaugePercent } from '../../shared/bmi-category';

@Component({
  selector: 'app-bmi-result',
  imports: [DecimalPipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './bmi-result.html',
  styleUrl: './bmi-result.scss',
})
export class BmiResult {
  readonly result = input.required<BmiResultModel>();

  protected readonly meta = CATEGORY_META;
  protected readonly gauge = GAUGE;
  protected readonly gaugePercent = gaugePercent;

  protected readonly markerLeft = computed(() => `${gaugePercent(this.result().record.bmi)}%`);
  protected readonly segmentFlex = (from: number, to: number): number => to - from;
}
