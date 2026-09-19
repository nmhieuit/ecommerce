import { http, HttpResponse } from 'msw';
import { afterEach, beforeAll, describe, expect, it, vi } from 'vitest';
import { ApiError, bffFetch, configureApiClient, configureAuthHooks } from '@ecommerce/api-client';
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

describe('bffFetch bearer token', () => {
  afterEach(() => configureAuthHooks({}));

  /**
   * Kiểm tra: khi có access token, `bffFetch` gắn header `Authorization: Bearer <token>`.
   * Lý do phải test: điểm duy nhất trong api-client đưa danh tính của người mua vào request; các
   * hook được cấu hình từ ứng dụng để package không phụ thuộc cơ chế đăng nhập hay nơi lưu token.
   * Task nguồn: spec 004 (SPA mua sắm tối thiểu) — FR-026 (đăng nhập, bổ sung cùng đợt cutover danh
   * tính của spec 014).
   */
  it('sends Authorization: Bearer when a token is available', async () => {
    let seen: string | null = null;
    server.use(
      http.get(`${GATEWAY_ORIGIN}/bff/products`, ({ request }) => {
        seen = request.headers.get('Authorization');
        return HttpResponse.json({ items: [] });
      }),
    );
    configureAuthHooks({ getAccessToken: () => 'abc' });

    await bffFetch('/bff/products');

    expect(seen).toBe('Bearer abc');
  });

  /**
   * Kiểm tra: chưa đăng nhập thì không gắn header `Authorization`.
   * Lý do phải test: không gửi "Bearer null/undefined" — token rác vẫn khiến gateway phản hồi 401
   * khó chẩn đoán.
   * Task nguồn: spec 004 (SPA mua sắm tối thiểu) — FR-026 (đăng nhập, bổ sung cùng đợt cutover danh
   * tính của spec 014).
   */
  it('sends no Authorization header when signed out', async () => {
    let seen: string | null = 'unset';
    server.use(
      http.get(`${GATEWAY_ORIGIN}/bff/products`, ({ request }) => {
        seen = request.headers.get('Authorization');
        return HttpResponse.json({ items: [] });
      }),
    );
    configureAuthHooks({ getAccessToken: () => null });

    await bffFetch('/bff/products');

    expect(seen).toBeNull();
  });

  /**
   * Kiểm tra: `onUnauthorized` chỉ được gọi khi request bị 401 mà có mang token; request không
   * token bị 401 thì không gọi.
   * Lý do phải test: 401 khi có token nghĩa là phiên đã hết hiệu lực (cần đưa về đăng nhập); 401
   * khi chưa có token chỉ là "chưa đăng nhập" và không được kích hoạt vòng đăng xuất lặp.
   * Task nguồn: spec 004 (SPA mua sắm tối thiểu) — FR-026 (đăng nhập, bổ sung cùng đợt cutover danh
   * tính của spec 014).
   */
  it('reports a 401 to the application only when the request carried a token', async () => {
    server.use(
      http.get(`${GATEWAY_ORIGIN}/bff/products`, () =>
        HttpResponse.json({ title: 'Unauthorized' }, { status: 401 }),
      ),
    );
    const onUnauthorized = vi.fn();

    configureAuthHooks({ getAccessToken: () => null, onUnauthorized });
    await bffFetch('/bff/products').catch(() => undefined);
    expect(onUnauthorized).not.toHaveBeenCalled();

    configureAuthHooks({ getAccessToken: () => 'abc', onUnauthorized });
    await bffFetch('/bff/products').catch(() => undefined);
    expect(onUnauthorized).toHaveBeenCalledTimes(1);
  });
});
