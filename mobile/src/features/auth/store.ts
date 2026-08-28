import { create } from 'zustand';

import { authApi, type Me, type RegisterInput } from '@/services/api/auth';
import { configureApiClient } from '@/services/api/client';
import { registerPushToken, unregisterPush } from '@/services/push';

import { tokenStore, type StoredTokens } from './tokenStore';

type AuthStatus = 'loading' | 'signedOut' | 'signedIn';

type AuthState = {
  status: AuthStatus;
  user: Me | null;
  tokens: StoredTokens | null;
  bootstrap: () => Promise<void>;
  login: (email: string, password: string) => Promise<void>;
  register: (input: RegisterInput) => Promise<void>;
  logout: () => Promise<void>;
};

export const useAuthStore = create<AuthState>((set) => ({
  status: 'loading',
  user: null,
  tokens: null,

  bootstrap: async () => {
    const tokens = await tokenStore.load();
    if (!tokens) {
      set({ status: 'signedOut', user: null, tokens: null });
      return;
    }

    set({ tokens });
    try {
      const user = await authApi.me();
      set({ status: 'signedIn', user });
      void registerPushToken();
    } catch {
      await tokenStore.clear();
      set({ status: 'signedOut', user: null, tokens: null });
    }
  },

  login: async (email, password) => {
    const t = await authApi.login(email, password);
    await afterAuth(set, { accessToken: t.accessToken, refreshToken: t.refreshToken });
  },

  register: async (input) => {
    const t = await authApi.register(input);
    await afterAuth(set, { accessToken: t.accessToken, refreshToken: t.refreshToken });
  },

  logout: async () => {
    await unregisterPush();
    await tokenStore.clear();
    set({ status: 'signedOut', user: null, tokens: null });
  },
}));

async function afterAuth(
  set: (partial: Partial<AuthState>) => void,
  tokens: StoredTokens,
): Promise<void> {
  await tokenStore.save(tokens);
  set({ tokens });
  const user = await authApi.me();
  set({ status: 'signedIn', user });
  void registerPushToken();
}

// Wire the transport client to the store once, at module load.
configureApiClient({
  getTokens: () => useAuthStore.getState().tokens,
  onTokensRefreshed: (tokens) => useAuthStore.setState({ tokens }),
  onSessionExpired: () => {
    void tokenStore.clear();
    useAuthStore.setState({ status: 'signedOut', user: null, tokens: null });
  },
});
