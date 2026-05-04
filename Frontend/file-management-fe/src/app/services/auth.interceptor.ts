import { HttpErrorResponse, HttpEvent, HttpHandlerFn, HttpRequest } from '@angular/common/http';
import { inject } from '@angular/core';
import { Observable, catchError, of, switchMap, throwError } from 'rxjs';
import { AuthService } from './auth.service';
import { Router } from '@angular/router';

export function authInterceptor(req: HttpRequest<unknown>, next: HttpHandlerFn): Observable<HttpEvent<unknown>> {
  const auth = inject(AuthService);
  const router = inject(Router);

  // Never try to refresh on auth endpoints (avoids refresh recursion loops).
  const isAuthEndpoint = /\/auth\/(login|register|refresh)(\?|$)/i.test(req.url);

  const accessToken = auth.getAccessToken();
  const authedReq = accessToken
    ? req.clone({ setHeaders: { Authorization: `Bearer ${accessToken}` } })
    : req;

  return next(authedReq).pipe(
    catchError((err: unknown) => {
      if (!(err instanceof HttpErrorResponse)) return throwError(() => err);

      // If unauthorized, try refresh once then replay.
      if (err.status !== 401) return throwError(() => err);
      if (isAuthEndpoint) return throwError(() => err);

      return auth.refresh().pipe(
        switchMap((ok) => {
          if (!ok) {
            auth.logout();
            router.navigateByUrl('/login');
            return throwError(() => err);
          }

          const token = auth.getAccessToken();
          if (!token) return throwError(() => err);

          const retryReq = req.clone({ setHeaders: { Authorization: `Bearer ${token}` } });
          return next(retryReq);
        }),
        catchError(() => {
          auth.logout();
          router.navigateByUrl('/login');
          return throwError(() => err);
        }),
      );
    }),
  );
}
