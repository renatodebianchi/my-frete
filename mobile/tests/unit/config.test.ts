import { config } from '../../src/lib/config';

describe('config', () => {
  it('exposes an API base URL ending in /v1', () => {
    expect(config.apiBaseUrl).toMatch(/\/v1$/);
  });
});
