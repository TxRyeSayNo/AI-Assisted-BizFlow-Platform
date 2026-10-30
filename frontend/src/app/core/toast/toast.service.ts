import { Injectable, signal } from '@angular/core';
import { ToastItem, ToastType } from './toast.types';

@Injectable({
  providedIn: 'root',
})
export class ToastService {
  readonly toasts = signal<ToastItem[]>([]);
  private readonly timeouts = new Map<string, any>();

  show(message: string, type: ToastType = 'info', duration = 4500, title?: string): string {
    if (!message || !message.trim()) return '';

    const cleanMessage = message.trim();

    // Check for identical existing toast to prevent spam
    const existing = this.toasts().find((t) => t.type === type && t.message === cleanMessage);
    if (existing) {
      this.resetTimer(existing.id, duration);
      return existing.id;
    }

    const id = `toast-${Date.now()}-${Math.random().toString(36).substring(2, 7)}`;
    const newToast: ToastItem = {
      id,
      type,
      message: cleanMessage,
      title,
      duration,
      createdAt: Date.now(),
      leaving: false,
    };

    // Keep at most 5 toasts visible simultaneously
    this.toasts.update((list) => {
      const trimmed = list.length >= 5 ? list.slice(list.length - 4) : list;
      return [...trimmed, newToast];
    });

    if (duration > 0) {
      const timer = setTimeout(() => {
        this.dismiss(id);
      }, duration);
      this.timeouts.set(id, timer);
    }

    return id;
  }

  error(message: string, duration = 5000, title?: string): string {
    return this.show(message, 'error', duration, title);
  }

  success(message: string, duration = 4000, title?: string): string {
    return this.show(message, 'success', duration, title);
  }

  info(message: string, duration = 4000, title?: string): string {
    return this.show(message, 'info', duration, title);
  }

  warning(message: string, duration = 4500, title?: string): string {
    return this.show(message, 'warning', duration, title);
  }

  dismiss(id: string): void {
    if (this.timeouts.has(id)) {
      clearTimeout(this.timeouts.get(id));
      this.timeouts.delete(id);
    }

    // Mark as leaving for smooth exit animation
    this.toasts.update((list) =>
      list.map((t) => (t.id === id ? { ...t, leaving: true } : t)),
    );

    setTimeout(() => {
      this.toasts.update((list) => list.filter((t) => t.id !== id));
    }, 220);
  }

  clear(): void {
    for (const timer of this.timeouts.values()) {
      clearTimeout(timer);
    }
    this.timeouts.clear();
    this.toasts.set([]);
  }

  private resetTimer(id: string, duration: number): void {
    if (this.timeouts.has(id)) {
      clearTimeout(this.timeouts.get(id));
      this.timeouts.delete(id);
    }
    if (duration > 0) {
      const timer = setTimeout(() => {
        this.dismiss(id);
      }, duration);
      this.timeouts.set(id, timer);
    }
  }
}

