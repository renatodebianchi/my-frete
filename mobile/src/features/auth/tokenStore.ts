import * as SecureStore from 'expo-secure-store';

const ACCESS = 'myfrete.accessToken';
const REFRESH = 'myfrete.refreshToken';

export type StoredTokens = { accessToken: string; refreshToken: string };

export const tokenStore = {
  async load(): Promise<StoredTokens | null> {
    try {
      const [accessToken, refreshToken] = await Promise.all([
        SecureStore.getItemAsync(ACCESS),
        SecureStore.getItemAsync(REFRESH),
      ]);
      return accessToken && refreshToken ? { accessToken, refreshToken } : null;
    } catch {
      return null;
    }
  },

  async save(tokens: StoredTokens): Promise<void> {
    await Promise.all([
      SecureStore.setItemAsync(ACCESS, tokens.accessToken),
      SecureStore.setItemAsync(REFRESH, tokens.refreshToken),
    ]);
  },

  async clear(): Promise<void> {
    await Promise.all([SecureStore.deleteItemAsync(ACCESS), SecureStore.deleteItemAsync(REFRESH)]);
  },
};
