import { apiFetch, ApiError, configureApiClient } from '../../src/services/api/client';

jest.mock('expo-crypto', () => ({ randomUUID: () => 'test-uuid' }));
jest.mock('expo-secure-store', () => ({
  getItemAsync: jest.fn(),
  setItemAsync: jest.fn(),
  deleteItemAsync: jest.fn(),
}));

describe('apiFetch', () => {
  const fetchMock = jest.fn();

  beforeEach(() => {
    fetchMock.mockReset();
    global.fetch = fetchMock as unknown as typeof fetch;
  });

  it('sends a correlation id and bearer token', async () => {
    configureApiClient({
      getTokens: () => ({ accessToken: 'access-1', refreshToken: 'refresh-1' }),
      onTokensRefreshed: jest.fn(),
      onSessionExpired: jest.fn(),
    });
    fetchMock.mockResolvedValueOnce(new Response(JSON.stringify({ ok: true }), { status: 200 }));

    await apiFetch('/accounts/me');

    const headers = fetchMock.mock.calls[0][1].headers as Record<string, string>;
    expect(headers['x-correlation-id']).toBe('test-uuid');
    expect(headers.authorization).toBe('Bearer access-1');
  });

  it('refreshes once on 401 and retries', async () => {
    const onTokensRefreshed = jest.fn();
    configureApiClient({
      getTokens: () => ({ accessToken: 'stale', refreshToken: 'refresh-1' }),
      onTokensRefreshed,
      onSessionExpired: jest.fn(),
    });

    fetchMock
      .mockResolvedValueOnce(new Response('', { status: 401 }))
      .mockResolvedValueOnce(
        new Response(JSON.stringify({ accessToken: 'fresh', refreshToken: 'refresh-2' }), { status: 200 }),
      )
      .mockResolvedValueOnce(new Response(JSON.stringify({ id: 'u1' }), { status: 200 }));

    const result = await apiFetch<{ id: string }>('/accounts/me');

    expect(result.id).toBe('u1');
    expect(fetchMock).toHaveBeenCalledTimes(3);
    expect(onTokensRefreshed).toHaveBeenCalledWith({ accessToken: 'fresh', refreshToken: 'refresh-2' });
  });

  it('throws ApiError with the problem code on failure', async () => {
    configureApiClient({
      getTokens: () => null,
      onTokensRefreshed: jest.fn(),
      onSessionExpired: jest.fn(),
    });
    fetchMock.mockResolvedValueOnce(
      new Response(JSON.stringify({ detail: 'nope', code: 'accounts.invalid_credentials' }), { status: 401 }),
    );

    await expect(apiFetch('/auth/login', { auth: false })).rejects.toMatchObject({
      name: 'ApiError',
      status: 401,
      code: 'accounts.invalid_credentials',
    });
    expect(ApiError).toBeDefined();
  });

  it('flattens a 422 validation errors map into the message and fieldErrors', async () => {
    configureApiClient({
      getTokens: () => null,
      onTokensRefreshed: jest.fn(),
      onSessionExpired: jest.fn(),
    });
    fetchMock.mockResolvedValueOnce(
      new Response(
        JSON.stringify({
          title: 'Validation failed',
          code: 'validation_error',
          errors: { Password: ['The length of \'Password\' must be at least 8 characters.'] },
        }),
        { status: 422 },
      ),
    );

    await expect(apiFetch('/auth/register', { auth: false })).rejects.toMatchObject({
      name: 'ApiError',
      status: 422,
      message: "The length of 'Password' must be at least 8 characters.",
      fieldErrors: { Password: ["The length of 'Password' must be at least 8 characters."] },
    });
  });
});
