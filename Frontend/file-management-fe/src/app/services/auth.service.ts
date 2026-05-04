import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { BehaviorSubject, Observable, catchError, finalize, map, of, shareReplay, tap } from 'rxjs';
import { environment } from '../../environments/environment';

export type AuthUser = { id: string; email: string; displayName?: string | null };

export type AuthTokens = {
  accessToken: string;
  refreshToken: string;
  user: AuthUser;
};

const REFRESH_KEY = 'fm.refreshToken';
const USER_KEY = 'fm.user';

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly apiUrl = `${environment.apiBaseUrl}/auth`;
  private accessToken: string | null = null;
  private authed$ = new BehaviorSubject<boolean>(false);
  private refreshInFlight$: Observable<boolean> | null = null;

  isAuthed$ = this.authed$.asObservable();

  constructor(private http: HttpClient) {
    const storedUser = this.getUser();
    const storedRefresh = this.getRefreshToken();
    this.authed$.next(!!storedUser && !!storedRefresh);
  }

  getAccessToken(): string | null {
    return this.accessToken;
  }

  getRefreshToken(): string | null {
    return localStorage.getItem(REFRESH_KEY);
  }

  getUser(): AuthUser | null {
    const raw = localStorage.getItem(USER_KEY);
    if (!raw) return null;
    try {
      return JSON.parse(raw) as AuthUser;
    } catch {
      return null;
    }
  }

  login(email: string, password: string): Observable<void> {
    return this.http
      .post<{ success: boolean; data?: AuthTokens; message?: string }>(`${this.apiUrl}/login`, { email, password })
      .pipe(
        map((res) => {
          if (!res.success || !res.data) throw new Error(res.message || 'Login failed');
          return res.data;
        }),
        tap((tokens) => this.setSession(tokens)),
        map(() => void 0),
      );
  }

  register(email: string, password: string, displayName?: string): Observable<void> {
    return this.http
      .post<{ success: boolean; data?: AuthTokens; message?: string }>(`${this.apiUrl}/register`, {
        email,
        password,
        displayName,
      })
      .pipe(
        map((res) => {
          if (!res.success || !res.data) throw new Error(res.message || 'Register failed');
          return res.data;
        }),
        tap((tokens) => this.setSession(tokens)),
        map(() => void 0),
      );
  }

  logout(): void {
    this.accessToken = null;
    localStorage.removeItem(REFRESH_KEY);
    localStorage.removeItem(USER_KEY);
    this.authed$.next(false);
  }

  refresh(): Observable<boolean> {
    const refreshToken = this.getRefreshToken();
    if (!refreshToken) return of(false);

    if (this.refreshInFlight$) return this.refreshInFlight$;

    this.refreshInFlight$ = this.http
      .post<{ success: boolean; data?: AuthTokens; message?: string }>(`${this.apiUrl}/refresh`, { refreshToken })
      .pipe(
        map((res) => {
          if (!res.success || !res.data?.accessToken || !res.data?.refreshToken || !res.data?.user) return false;
          this.setSession(res.data);
          return true;
        }),
        catchError(() => of(false)),
        finalize(() => {
          this.refreshInFlight$ = null;
        }),
        shareReplay({ bufferSize: 1, refCount: false }),
      );

    return this.refreshInFlight$;
  }

  private setSession(tokens: AuthTokens): void {
    this.accessToken = tokens.accessToken;
    localStorage.setItem(REFRESH_KEY, tokens.refreshToken);
    localStorage.setItem(USER_KEY, JSON.stringify(tokens.user));
    this.authed$.next(true);
  }
}
