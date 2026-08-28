import Constants from 'expo-constants';

type AppConfig = {
  apiBaseUrl: string;
};

const extra = (Constants.expoConfig?.extra ?? {}) as Partial<AppConfig>;

export const config: AppConfig = {
  apiBaseUrl: process.env.EXPO_PUBLIC_API_BASE_URL ?? extra.apiBaseUrl ?? 'http://localhost:8080/v1',
};
