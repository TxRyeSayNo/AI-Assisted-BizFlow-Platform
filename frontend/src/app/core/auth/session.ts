import { HttpClient } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import {
  Observable,
  catchError,
  defer,
  finalize,
  firstValueFrom,
  shareReplay,
  tap,
  throwError,
} from 'rxjs';

export interface LoginRequest {
  identifier: string;
  password: string;
  tenantKey?: string;
}

export interface SessionIdentity {
  userId: string;
  fullName: string;
  tenantId: string | null;
  tenantName: string | null;
  permissions: string[];
}

export interface LoginResponse {
  accessToken: string;
  refreshToken: string;
  expiresAt: string;
  session: SessionIdentity;
}

@Injectable({ providedIn: 'root' })
export class SessionService {
  private readonly http = inject(HttpClient);
  private readonly response = signal<LoginResponse | null>(null);
  private refreshRequest: Observable<LoginResponse> | null = null;
  private generation = 0;
  readonly identity = computed(() => this.response()?.session ?? null);
  readonly signedIn = computed(() => this.identity() !== null);
  // Credentials and tokens stay in memory; never persist them in web storage.
  login(request: LoginRequest) {
    return defer(() => {
      this.clear();
      const generation = this.generation;
      return this.http.post<LoginResponse>('/api/v1/auth/login', request).pipe(
        tap((response) => {
          if (generation === this.generation) this.response.set(response);
        }),
      );
    });
  }

  accessToken(): string | null {
    return this.response()?.accessToken ?? null;
  }

  sessionGeneration(): number {
    return this.generation;
  }

  async realtimeAccessToken(): Promise<string> {
    const current = this.response();
    if (!current) throw new Error('No active session.');
    if (Date.parse(current.expiresAt) > Date.now() + 30_000) return current.accessToken;
    // Shares the single-use refresh rotation used by ordinary HTTP requests.
    return (await firstValueFrom(this.refresh())).accessToken;
  }

  clear(): void {
    this.generation++;
    this.response.set(null);
    this.refreshRequest = null;
  }

  refresh(): Observable<LoginResponse> {
    if (this.refreshRequest) return this.refreshRequest;
    const refreshToken = this.response()?.refreshToken;
    if (!refreshToken) return throwError(() => new Error('No active session.'));
    const generation = this.generation;
    const request$: Observable<LoginResponse> = this.http
      .post<LoginResponse>('/api/v1/auth/refresh', { refreshToken })
      .pipe(
        tap((response) => {
          if (generation !== this.generation) throw new Error('The session changed.');
          this.response.set(response);
        }),
        catchError((error: unknown) => {
          if (generation === this.generation) this.clear();
          return throwError(() => error);
        }),
        finalize(() => {
          if (this.refreshRequest === request$) this.refreshRequest = null;
        }),
        // Share one rotation across simultaneous 401s; never replay a single-use refresh token.
        shareReplay({ bufferSize: 1, refCount: false }),
      );
    this.refreshRequest = request$;
    return request$;
  }

  hasPermission(permission: string): boolean {
    return this.identity()?.permissions.includes(permission) ?? false;
  }
}
