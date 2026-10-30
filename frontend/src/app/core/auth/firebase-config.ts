import { InjectionToken } from '@angular/core';

export interface FirebaseClientConfig {
  apiKey: string;
  authDomain: string;
  projectId: string;
  storageBucket?: string;
  messagingSenderId?: string;
  appId?: string;
  actionUrl?: string;
}

export const FIREBASE_CLIENT_CONFIG = new InjectionToken<FirebaseClientConfig>(
  'FIREBASE_CLIENT_CONFIG',
  {
    providedIn: 'root',
    factory: () => {
      const globalConfig =
        typeof window !== 'undefined'
          ? (window as unknown as { __BIZFLOW_FIREBASE_CONFIG__?: Partial<FirebaseClientConfig> })
              .__BIZFLOW_FIREBASE_CONFIG__
          : undefined;

      return {
        apiKey: globalConfig?.apiKey || '',
        authDomain: globalConfig?.authDomain || '',
        projectId: globalConfig?.projectId || '',
        storageBucket: globalConfig?.storageBucket || '',
        messagingSenderId: globalConfig?.messagingSenderId || '',
        appId: globalConfig?.appId || '',
        actionUrl:
          globalConfig?.actionUrl ||
          (typeof window !== 'undefined'
            ? `${window.location.origin}/login`
            : 'http://localhost:4200/login'),
      };
    },
  },
);
