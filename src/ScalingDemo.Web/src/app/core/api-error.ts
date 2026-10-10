import { HttpErrorResponse } from '@angular/common/http';

interface ProblemBody {
  title?: string;
  detail?: string;
  errors?: Record<string, string[]>;
}

const FALLBACK_MESSAGE = 'Something went wrong. Please try again.';
const OFFLINE_MESSAGE = 'Cannot reach the server. Check your connection and try again.';

/** Turns an ASP.NET Core ProblemDetails / ValidationProblemDetails response into one readable sentence. */
export function toMessage(error: unknown): string {
  if (!(error instanceof HttpErrorResponse)) {
    return FALLBACK_MESSAGE;
  }
  if (error.status === 0) {
    return OFFLINE_MESSAGE;
  }

  const body = error.error as ProblemBody | null;
  const validation = Object.values(body?.errors ?? {}).flat();

  return validation.length ? validation.join(' ') : (body?.detail ?? body?.title ?? FALLBACK_MESSAGE);
}
