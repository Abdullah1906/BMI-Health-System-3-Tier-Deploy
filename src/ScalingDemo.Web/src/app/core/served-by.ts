import { HttpEvent, HttpInterceptorFn, HttpResponse } from '@angular/common/http';
import { inject, Injectable, signal } from '@angular/core';
import { tap } from 'rxjs';

/** Remembers which backend replica answered last, so the footer can show load balancing at work. */
@Injectable({ providedIn: 'root' })
export class ServerInfo {
  readonly servedBy = signal<string | null>(null);
}

export const servedByInterceptor: HttpInterceptorFn = (request, next) => {
  const info = inject(ServerInfo);

  function rememberServer(event: HttpEvent<unknown>): void {
    const host = event instanceof HttpResponse ? event.headers.get('X-Served-By') : null;
    if (host) {
      info.servedBy.set(host);
    }
  }

  return next(request).pipe(tap(rememberServer));
};
