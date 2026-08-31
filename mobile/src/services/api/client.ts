import * as Crypto from 'expo-crypto';

import { config } from '@/lib/config';
import { tokenStore, type StoredTokens } from '@/features/auth/tokenStore';

export class ApiError extends Error {
  constructor(
    readonly status: number,
    readonly code: string | undefined,
    message: string,
    readonly body: unknown,
    /** Field -> messages, from a 422 validation ProblemDetails (`errors` map). */
    readonly fieldErrors?: Record<string, string[]>,
  ) {
    super(message);
    this.name = 'ApiError';
  }
}

type Tokens = StoredTokens | null;

/**
 * Callbacks let the auth store own token state while the client owns transport concerns
 * (correlation id, bearer, single refresh-and-retry on 401).
 */
type ClientHooks = {
  getTokens: () => Tokens;
  onTokensRefreshed: (tokens: StoredTokens) => void;
  onSessionExpired: () => void;
};

let hooks: ClientHooks = {
  getTokens: () => null,
  onTokensRefreshed: () => {},
  onSessionExpired: () => {},
};

export function configureApiClient(next: ClientHooks): void {
  hooks = next;
}

let refreshInFlight: Promise<StoredTokens | null> | null = null;

async function refreshTokens(): Promise<StoredTokens | null> {
  const current = hooks.getTokens();
  if (!current) return null;

  refreshInFlight ??= (async () => {
    const res = await fetch(`${config.apiBaseUrl}/auth/refresh`, {
      method: 'POST',
      headers: { 'content-type': 'application/json', 'x-correlation-id': Crypto.randomUUID() },
      body: JSON.stringify({ refreshToken: current.refreshToken }),
    });
    if (!res.ok) return null;
    const data = (await res.json()) as { accessToken: string; refreshToken: string };
    const next = { accessToken: data.accessToken, refreshToken: data.refreshToken };
    await tokenStore.save(next);
    hooks.onTokensRefreshed(next);
    return next;
  })().finally(() => {
    refreshInFlight = null;
  });

  return refreshInFlight;
}

export async function apiFetch<T>(
  path: string,
  init: RequestInit & { auth?: boolean } = {},
): Promise<T> {
  const { auth = true, headers, ...rest } = init;

  const doRequest = (accessToken?: string) =>
    fetch(`${config.apiBaseUrl}${path}`, {
      ...rest,
      headers: {
        'content-type': 'application/json',
        'x-correlation-id': Crypto.randomUUID(),
        ...(accessToken ? { authorization: `Bearer ${accessToken}` } : {}),
        ...headers,
      },
    });

  let response = await doRequest(auth ? hooks.getTokens()?.accessToken : undefined);

  if (response.status === 401 && auth) {
    const refreshed = await refreshTokens();
    if (refreshed) {
      response = await doRequest(refreshed.accessToken);
    } else {
      hooks.onSessionExpired();
    }
  }

  const text = await response.text();
  const body: unknown = text.length ? safeJson(text) : null;

  if (!response.ok) {
    const problem = (body ?? {}) as {
      detail?: string;
      title?: string;
      code?: string;
      errors?: Record<string, string[]>;
    };
    const fieldErrors =
      problem.errors && typeof problem.errors === 'object' ? problem.errors : undefined;
    const flattened = fieldErrors ? Object.values(fieldErrors).flat() : [];
    const message =
      flattened.length > 0
        ? flattened.join('\n')
        : (problem.detail ?? problem.title ?? `Request failed (${response.status})`);
    throw new ApiError(response.status, problem.code, message, body, fieldErrors);
  }

  return body as T;
}

function safeJson(text: string): unknown {
  try {
    return JSON.parse(text);
  } catch {
    return text;
  }
}
