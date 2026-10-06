import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, map, of, switchMap, throwError } from 'rxjs';
import { SessionService } from './session';

export const authInterceptor: HttpInterceptorFn = (request, next) => {
  // Never attach a bearer token to external URLs, assets, or credential endpoints.
  if (!request.url.startsWith('/api/v1/') || request.url.startsWith('/api/v1/auth/')) {
    return next(request);
  }
  const session = inject(SessionService);
  const router = inject(Router);
  const token = session.accessToken();
  if (!token) return next(request);
  const generation = session.sessionGeneration();
  const authenticated = (accessToken: string) =>
    request.clone({ setHeaders: { Authorization: `Bearer ${accessToken}` } });

  return next(authenticated(token)).pipe(
    catchError((error: unknown) => {
      if (
        !(error instanceof HttpErrorResponse) ||
        error.status !== 401 ||
        generation !== session.sessionGeneration()
      ) {
        return throwError(() => error);
      }
      // A delayed 401 may arrive after another request already completed rotation.
      const current = session.accessToken();
      const fresh =
        current && current !== token
          ? of(current)
          : session.refresh().pipe(map((response) => response.accessToken));
      return fresh.pipe(
        switchMap((accessToken) => next(authenticated(accessToken))),
        catchError((failure: unknown) => {
          // Do not clear a newly signed-in user's session due to an older request's failure.
          if (
            generation === session.sessionGeneration() &&
            failure instanceof HttpErrorResponse &&
            failure.status === 401
          ) {
            session.clear();
            void router.navigateByUrl('/login');
          } else if (!session.signedIn()) {
            void router.navigateByUrl('/login');
          }
          return throwError(() => failure);
        }),
      );
    }),
  );
};
