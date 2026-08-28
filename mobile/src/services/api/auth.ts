import { apiFetch } from './client';

export type AuthTokens = {
  accessToken: string;
  refreshToken: string;
  expiresInSeconds: number;
};

export type Me = {
  id: string;
  name: string;
  email: string;
  phone: string;
  roles: string[];
  professional?: {
    maxLoadKg: number;
    immediateAvailability: boolean;
    verificationStatus: string;
  };
};

export type RegisterInput = {
  name: string;
  email: string;
  phone: string;
  password: string;
  roles: ('client' | 'professional')[];
  maxLoadKg?: number;
};

export const authApi = {
  register: (input: RegisterInput) =>
    apiFetch<AuthTokens>('/auth/register', { method: 'POST', body: JSON.stringify(input), auth: false }),

  login: (email: string, password: string) =>
    apiFetch<AuthTokens>('/auth/login', {
      method: 'POST',
      body: JSON.stringify({ email, password }),
      auth: false,
    }),

  me: () => apiFetch<Me>('/accounts/me'),
};
