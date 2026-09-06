import { http, HttpResponse } from 'msw';
import { beforeAll, describe, expect, it } from 'vitest';
import { ApiError, bffFetch, configureApiClient } from '@ecommerce/api-client';
import { server } from '../msw/server';

/**
 * 016-correlation-id-propagation spec US2 AC2, contracts/spa-correlation-visibility-contract.md:
 * the correlation ID a failed request carries must be readable by code, not only visible to a
 * person who happens to open DevTools — that is what lets a future error report or support ticket
 * quote it as the key into the backend's own logs.
 */

const GATEWAY_ORIGIN = 'http://fetcher-test.gateway.test';

beforeAll(() => {
  configureApiClient({ baseUrl: GATEWAY_ORIGIN });
});

describe('bffFetch correlation ID', () => {
  it('attaches the response X-Correlation-Id to the thrown ApiError', async () => {
    server.use(
      http.get(`${GATEWAY_ORIGIN}/bff/products`, () =>
        HttpResponse.json(
          { title: 'Bad Gateway' },
          { status: 502, headers: { 'X-Correlation-Id': 'test-cid-123' } },
        ),
      ),
    );

    const error = await bffFetch('/bff/products').catch((caught: unknown) => caught);

    expect(error).toBeInstanceOf(ApiError);
    expect((error as ApiError).correlationId).toBe('test-cid-123');
  });

  it('is null when the failed response carries no correlation ID', async () => {
    server.use(
      http.get(`${GATEWAY_ORIGIN}/bff/products`, () =>
        HttpResponse.json({ title: 'Bad Gateway' }, { status: 502 }),
      ),
    );

    const error = await bffFetch('/bff/products').catch((caught: unknown) => caught);

    expect(error).toBeInstanceOf(ApiError);
    expect((error as ApiError).correlationId).toBeNull();
  });
});
