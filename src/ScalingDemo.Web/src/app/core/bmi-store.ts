import { computed, inject, Injectable, signal } from '@angular/core';
import { finalize } from 'rxjs';
import { toMessage } from './api-error';
import { BmiApi } from './bmi-api';
import { BmiRecord, BmiRequest, BmiResult, BmiStats, PagedResult } from './models';

/** Single source of UI state. Components read signals and call methods; nothing else talks to the API. */
@Injectable({ providedIn: 'root' })
export class BmiStore {
  private readonly api = inject(BmiApi);

  readonly pageSize = 5;

  readonly latest = signal<BmiResult | null>(null);
  readonly history = signal<PagedResult<BmiRecord> | null>(null);
  readonly stats = signal<BmiStats | null>(null);
  readonly page = signal(1);

  readonly submitting = signal(false);
  readonly formError = signal<string | null>(null);
  readonly loadError = signal<string | null>(null);

  readonly totalPages = computed(() =>
    Math.max(1, Math.ceil((this.history()?.totalCount ?? 0) / this.pageSize)),
  );

  calculate(request: BmiRequest): void {
    this.submitting.set(true);
    this.formError.set(null);

    this.api
      .create(request)
      .pipe(finalize(this.endSubmit))
      .subscribe({ next: this.onCalculated, error: this.onCalculateFailed });
  }

  refresh(): void {
    this.loadHistory(this.page());
    this.loadStats();
  }

  loadHistory(page: number): void {
    this.page.set(page);
    this.api.list(page, this.pageSize).subscribe({ next: this.onHistory, error: this.onLoadFailed });
  }

  loadStats(): void {
    this.api.stats().subscribe({ next: this.onStats, error: this.onLoadFailed });
  }

  remove(id: number): void {
    this.api.remove(id).subscribe({ next: this.onRemoved, error: this.onLoadFailed });
  }

  private readonly endSubmit = () => this.submitting.set(false);

  private readonly onCalculated = (result: BmiResult) => {
    this.latest.set(result);
    this.page.set(1);
    this.refresh();
  };

  private readonly onCalculateFailed = (error: unknown) => this.formError.set(toMessage(error));

  private readonly onHistory = (history: PagedResult<BmiRecord>) => {
    this.history.set(history);
    this.loadError.set(null);
  };

  private readonly onStats = (stats: BmiStats) => {
    this.stats.set(stats);
    this.loadError.set(null);
  };

  private readonly onRemoved = () => {
    const wasOnlyItemOnPage = (this.history()?.items.length ?? 0) <= 1;
    if (wasOnlyItemOnPage && this.page() > 1) {
      this.page.update((page) => page - 1);
    }
    this.refresh();
  };

  private readonly onLoadFailed = (error: unknown) => this.loadError.set(toMessage(error));
}
