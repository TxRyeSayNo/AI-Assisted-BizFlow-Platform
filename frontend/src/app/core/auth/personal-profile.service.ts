import { HttpClient } from '@angular/common/http';
import { Injectable, inject, signal } from '@angular/core';
import { Subscription, retry, throwError, timeout, timer } from 'rxjs';

export interface PersonalProfile {
  userId: string;
  fullName: string;
  nickname: string;
  employeeCode: string;
  email: string;
  departmentName: string | null;
  tenantName: string | null;
  tenantKey: string | null;
  roles: string[];
  hasAvatar: boolean;
  createdAt: string;
  minimumPasswordLength: number;
}

@Injectable({ providedIn: 'root' })
export class PersonalProfileService {
  private readonly http = inject(HttpClient);
  readonly profile = signal<PersonalProfile | null>(null);
  readonly avatarUrl = signal<string | null>(null);
  readonly loading = signal(false);
  readonly failure = signal(false);
  private owner: string | null = null;
  private request?: Subscription;
  private avatarRequest?: Subscription;

  load(userId: string, refresh = false): void {
    if (this.owner === userId) {
      if ((refresh || this.failure()) && !this.loading()) this.refresh();
      return;
    }
    this.clear();
    this.owner = userId;
    this.refresh();
  }

  refresh(): void {
    const owner = this.owner;
    if (!owner) return;
    this.request?.unsubscribe();
    this.loading.set(true);
    this.failure.set(false);
    this.request = this.http
      .get<PersonalProfile>('/api/v1/auth/profile')
      .pipe(
        timeout(15000),
        retry({
          count: 1,
          delay: (error) =>
            error.status === 0 || error.status >= 500 || error.name === 'TimeoutError'
              ? timer(500)
              : throwError(() => error),
        }),
      )
      .subscribe({
        next: (profile) => {
          if (owner !== this.owner) return;
          this.profile.set(profile);
          this.loading.set(false);
          this.refreshAvatar(profile.hasAvatar);
        },
        error: () => {
          if (owner === this.owner) {
            this.loading.set(false);
            this.failure.set(true);
          }
        },
      });
  }

  refreshAvatar(hasAvatar: boolean): void {
    this.avatarRequest?.unsubscribe();
    this.releaseAvatar();
    if (!hasAvatar) return;
    const owner = this.owner;
    this.avatarRequest = this.http
      .get('/api/v1/auth/profile/avatar', { responseType: 'blob' })
      .subscribe({
        next: (blob) => {
          if (owner === this.owner) this.avatarUrl.set(URL.createObjectURL(blob));
        },
        error: () => {
          /* Keep initials when the image is unavailable. */
        },
      });
  }

  updateNickname(nickname: string): void {
    this.profile.update((profile) => (profile ? { ...profile, nickname } : profile));
  }

  clear(): void {
    this.owner = null;
    this.request?.unsubscribe();
    this.avatarRequest?.unsubscribe();
    this.releaseAvatar();
    this.profile.set(null);
    this.loading.set(false);
    this.failure.set(false);
  }

  private releaseAvatar(): void {
    const url = this.avatarUrl();
    if (url) URL.revokeObjectURL(url);
    this.avatarUrl.set(null);
  }
}
