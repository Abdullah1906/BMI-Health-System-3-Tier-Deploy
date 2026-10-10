import { ChangeDetectionStrategy, Component, inject, OnInit } from '@angular/core';
import { BmiStore } from './core/bmi-store';
import { ServerInfo } from './core/served-by';
import { BmiForm } from './features/bmi-form/bmi-form';
import { BmiHistory } from './features/bmi-history/bmi-history';
import { BmiResult } from './features/bmi-result/bmi-result';
import { BmiStats } from './features/bmi-stats/bmi-stats';

@Component({
  selector: 'app-root',
  imports: [BmiForm, BmiResult, BmiStats, BmiHistory],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './app.html',
})
export class App implements OnInit {
  protected readonly store = inject(BmiStore);
  protected readonly server = inject(ServerInfo);

  ngOnInit(): void {
    this.store.refresh();
  }
}
