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
        apiKey: globalConfig?.apiKey || 'AIzaSyD2TUo2v-6TY64HpP60LwmbhK9JMAomOBg',
        authDomain: globalConfig?.authDomain || 'bizflow-8235a.firebaseapp.com',
        projectId: globalConfig?.projectId || 'bizflow-8235a',
        storageBucket: globalConfig?.storageBucket || 'bizflow-8235a.firebasestorage.app',
        messagingSenderId: globalConfig?.messagingSenderId || '241192427150',
        appId: globalConfig?.appId || '1:241192427150:web:fd942eb7233567e973f3df',
        actionUrl:
          globalConfig?.actionUrl ||
          (typeof window !== 'undefined'
            ? `${window.location.origin}/login`
            : 'http://localhost:4200/login'),
      };
    },
  },
);
