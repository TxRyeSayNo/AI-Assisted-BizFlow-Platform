import { DatePipe } from '@angular/common';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Component, DestroyRef, effect, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { finalize } from 'rxjs';
import { PersonalProfileService } from '../../core/auth/personal-profile.service';
import { roleLabels } from '../../core/auth/user-display';
import { SessionService } from '../../core/auth/session';
import { ToastService } from '../../core/toast/toast.service';

@Component({
  selector: 'bf-personal-profile',
  imports: [ReactiveFormsModule, RouterLink, DatePipe],
  templateUrl: './personal-profile.html',
  styleUrl: './personal-profile.scss',
})
export class PersonalProfile {
  readonly roleLabels = roleLabels;
  readonly state = inject(PersonalProfileService);
  private readonly session = inject(SessionService);
  private readonly http = inject(HttpClient);
  private readonly toast = inject(ToastService);
  private readonly router = inject(Router);
  private readonly destroy = inject(DestroyRef);
  private readonly fb = inject(FormBuilder);
  readonly savingName = signal(false);
  readonly savingAvatar = signal(false);
  readonly savingPassword = signal(false);
  readonly nameError = signal('');
  readonly avatarError = signal('');
  readonly passwordError = signal('');
  readonly showPasswords = signal(false);
  readonly nameForm = this.fb.nonNullable.group({
    nickname: ['', [Validators.required, Validators.maxLength(200), Validators.pattern(/\S/)]],
  });
  readonly passwordForm = this.fb.nonNullable.group({
    currentPassword: ['', [Validators.required, Validators.maxLength(1024)]],
    newPassword: ['', [Validators.required, Validators.minLength(12), Validators.maxLength(1024)]],
    confirmPassword: ['', Validators.required],
  });

  constructor() {
    const id = this.session.identity()?.userId;
    if (id) this.state.load(id, true);
    effect(() => {
      const profile = this.state.profile();
      if (profile && !this.nameForm.dirty)
        this.nameForm.controls.nickname.setValue(profile.nickname ?? profile.fullName);
      if (profile) {
        this.passwordForm.controls.newPassword.setValidators([
          Validators.required,
          Validators.minLength(profile.minimumPasswordLength),
          Validators.maxLength(1024),
        ]);
        this.passwordForm.controls.newPassword.updateValueAndValidity({ emitEvent: false });
      }
    });
  }

  saveName(): void {
    this.nameForm.markAllAsTouched();
    if (this.nameForm.invalid || this.savingName()) return;
    const nickname = this.nameForm.getRawValue().nickname.trim();
    this.savingName.set(true);
    this.nameError.set('');
    this.http
      .patch('/api/v1/auth/profile', { nickname })
      .pipe(
        takeUntilDestroyed(this.destroy),
        finalize(() => this.savingName.set(false)),
      )
      .subscribe({
        next: () => {
          this.state.updateNickname(nickname);
          this.session.updateNickname(nickname);
          this.nameForm.markAsPristine();
          this.toast.success('Đã cập nhật thông tin cá nhân.');
        },
        error: (error) =>
          this.nameError.set(
            this.message(error, 'Không thể cập nhật thông tin. Vui lòng thử lại.'),
          ),
      });
  }

  async chooseAvatar(event: Event): Promise<void> {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    input.value = '';
    if (!file || this.savingAvatar()) return;
    this.avatarError.set('');
    if (
      !['image/png', 'image/jpeg', 'image/webp'].includes(file.type) ||
      file.size > 5 * 1024 * 1024
    ) {
      this.avatarError.set('Chọn ảnh PNG, JPEG hoặc WebP có dung lượng tối đa 5 MB.');
      return;
    }
    this.savingAvatar.set(true);
    try {
      const image = await createImageBitmap(file);
      try {
        if (image.width * image.height > 40_000_000) throw new Error('Image too large');
        const canvas = document.createElement('canvas');
        const scale = Math.min(1, 512 / Math.max(image.width, image.height));
        canvas.width = Math.max(1, Math.round(image.width * scale));
        canvas.height = Math.max(1, Math.round(image.height * scale));
        const ctx = canvas.getContext('2d');
        if (!ctx) throw new Error('Canvas unavailable');
        ctx.drawImage(image, 0, 0, canvas.width, canvas.height);
        const blob = await new Promise<Blob>((resolve, reject) =>
          canvas.toBlob(
            (value) => (value ? resolve(value) : reject(new Error('Invalid image'))),
            'image/png',
          ),
        );
        if (this.destroy.destroyed) return;
        const data = new FormData();
        data.append('file', blob, 'avatar.png');
        this.http
          .put('/api/v1/auth/profile/avatar', data)
          .pipe(
            takeUntilDestroyed(this.destroy),
            finalize(() => this.savingAvatar.set(false)),
          )
          .subscribe({
            next: () => {
              this.state.refresh();
              this.toast.success('Đã cập nhật ảnh đại diện.');
            },
            error: (error) =>
              this.avatarError.set(this.message(error, 'Không thể cập nhật ảnh đại diện.')),
          });
      } finally {
        image.close();
      }
    } catch {
      this.avatarError.set('Không đọc được ảnh. Vui lòng chọn ảnh khác hoặc giảm kích thước ảnh.');
      this.savingAvatar.set(false);
    }
  }

  removeAvatar(): void {
    if (this.savingAvatar()) return;
    this.savingAvatar.set(true);
    this.avatarError.set('');
    this.http
      .delete('/api/v1/auth/profile/avatar')
      .pipe(
        takeUntilDestroyed(this.destroy),
        finalize(() => this.savingAvatar.set(false)),
      )
      .subscribe({
        next: () => {
          this.state.refresh();
          this.toast.success('Đã xóa ảnh đại diện.');
        },
        error: (error) => this.avatarError.set(this.message(error, 'Không thể xóa ảnh đại diện.')),
      });
  }

  changePassword(): void {
    this.passwordForm.markAllAsTouched();
    this.passwordError.set('');
    if (this.passwordForm.invalid || this.savingPassword()) return;
    const { currentPassword, newPassword, confirmPassword } = this.passwordForm.getRawValue();
    if (newPassword !== confirmPassword) {
      this.passwordError.set('Xác nhận mật khẩu mới chưa khớp.');
      return;
    }
    if (currentPassword === newPassword) {
      this.passwordError.set('Mật khẩu mới phải khác mật khẩu hiện tại.');
      return;
    }
    this.savingPassword.set(true);
    this.http
      .post('/api/v1/auth/profile/change-password', { currentPassword, newPassword })
      .pipe(
        takeUntilDestroyed(this.destroy),
        finalize(() => this.savingPassword.set(false)),
      )
      .subscribe({
        next: () => {
          this.passwordForm.reset();
          this.session.clear();
          this.state.clear();
          this.toast.success(
            'Đã đổi mật khẩu và đăng xuất các phiên cũ. Hãy đăng nhập bằng mật khẩu mới.',
          );
          void this.router.navigate(['/login']);
        },
        error: (error) => {
          this.passwordForm.reset();
          this.passwordError.set(this.message(error, 'Không thể đổi mật khẩu. Vui lòng thử lại.'));
        },
      });
  }

  private message(error: HttpErrorResponse, fallback: string): string {
    if (error.status === 429) return 'Bạn thao tác quá nhiều lần. Vui lòng chờ rồi thử lại.';
    return error.status === 400 || error.status === 422
      ? error.error?.message || fallback
      : fallback;
  }
}
