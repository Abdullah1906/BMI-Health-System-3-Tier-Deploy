import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { BmiRecord, BmiRequest, BmiResult, BmiStats, PagedResult } from './models';

/** Thin HTTP wrapper. URLs are relative: nginx proxies /api in containers, `ng serve` proxies it in dev. */
@Injectable({ providedIn: 'root' })
export class BmiApi {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = '/api/bmi';

  create(request: BmiRequest) {
    return this.http.post<BmiResult>(this.baseUrl, request);
  }

  list(page: number, pageSize: number) {
    return this.http.get<PagedResult<BmiRecord>>(this.baseUrl, { params: { page, pageSize } });
  }

  stats() {
    return this.http.get<BmiStats>(`${this.baseUrl}/stats`);
  }

  remove(id: number) {
    return this.http.delete<void>(`${this.baseUrl}/${id}`);
  }
}
