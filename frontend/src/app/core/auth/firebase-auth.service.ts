import { Injectable, inject } from '@angular/core';
import { FirebaseApp, getApp, getApps, initializeApp } from 'firebase/app';
import {
  Auth,
  getAuth,
  isSignInWithEmailLink,
  sendSignInLinkToEmail,
  signInWithEmailLink,
} from 'firebase/auth';
import { FIREBASE_CLIENT_CONFIG } from './firebase-config';

@Injectable({ providedIn: 'root' })
export class FirebaseAuthService {
  private readonly config = inject(FIREBASE_CLIENT_CONFIG);
  private app: FirebaseApp | null = null;
  private auth: Auth | null = null;

  private static readonly STORAGE_KEY = 'bizflow_firebase_email_for_sign_in';

  constructor() {
    this.initIfConfigured();
  }

  isConfigured(): boolean {
    return Boolean(
      this.config.apiKey &&
      this.config.apiKey !== 'REPLACE_ME' &&
      this.config.projectId &&
      this.config.projectId !== 'REPLACE_ME',
    );
  }

  private initIfConfigured(): void {
    if (!this.isConfigured() || typeof window === 'undefined') {
      return;
    }
    try {
      this.app = getApps().length > 0 ? getApp() : initializeApp(this.config);
      this.auth = getAuth(this.app);
    } catch (e) {
      console.warn('Firebase initialization skipped or failed:', e);
    }
  }

  async sendSignInLink(
    email: string,
    actionUrl?: string,
  ): Promise<{ success: boolean; message?: string }> {
    if (!this.auth) {
      this.initIfConfigured();
    }
    if (!this.auth) {
      throw new Error('FIREBASE_NOT_CONFIGURED');
    }

    const targetUrl = actionUrl || this.config.actionUrl || window.location.href;
    const actionCodeSettings = {
      url: targetUrl,
      handleCodeInApp: true,
    };

    await sendSignInLinkToEmail(this.auth, email, actionCodeSettings);
    this.storeEmail(email);
    return { success: true };
  }

  isSignInLink(url?: string): boolean {
    if (!this.auth) {
      this.initIfConfigured();
    }
    if (!this.auth || typeof window === 'undefined') {
      return false;
    }
    const checkUrl = url || window.location.href;
    return isSignInWithEmailLink(this.auth, checkUrl);
  }

  async completeSignInWithEmailLink(
    email?: string,
    url?: string,
  ): Promise<{ idToken: string; email: string }> {
    if (!this.auth) {
      this.initIfConfigured();
    }
    if (!this.auth) {
      throw new Error('FIREBASE_NOT_CONFIGURED');
    }

    const emailToUse = email || this.getStoredEmail();
    if (!emailToUse) {
      throw new Error('EMAIL_REQUIRED_FOR_SIGN_IN');
    }

    const checkUrl = url || window.location.href;
    const credential = await signInWithEmailLink(this.auth, emailToUse, checkUrl);
    this.clearStoredEmail();

    const idToken = await credential.user.getIdToken();
    return {
      idToken,
      email: credential.user.email || emailToUse,
    };
  }

  storeEmail(email: string): void {
    if (typeof window !== 'undefined') {
      try {
        window.localStorage.setItem(FirebaseAuthService.STORAGE_KEY, email);
      } catch {
        // Safe guard for restricted storage environments
      }
    }
  }

  getStoredEmail(): string | null {
    if (typeof window !== 'undefined') {
      try {
        return window.localStorage.getItem(FirebaseAuthService.STORAGE_KEY);
      } catch {
        return null;
      }
    }
    return null;
  }

  clearStoredEmail(): void {
    if (typeof window !== 'undefined') {
      try {
        window.localStorage.removeItem(FirebaseAuthService.STORAGE_KEY);
      } catch {
        // Safe guard
      }
    }
  }
}
