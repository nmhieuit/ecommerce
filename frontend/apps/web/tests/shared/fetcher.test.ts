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
  /**
   * Kiểm tra: khi request lỗi, `ApiError` do `bffFetch` ném ra mang giá trị header
   * `X-Correlation-Id` của phản hồi.
   * Lý do: mã tương quan phải đọc được bằng code (không chỉ thấy trong DevTools) để báo lỗi/ticket
   * hỗ trợ trích dẫn được và tra ngược log backend.
   * Task nguồn: spec 016 (correlation ID xuyên suốt) — US2 AC2
   * (contracts/spa-correlation-visibility-contract.md).
   */
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

    // toBeInstanceOf(lớp): xanh khi đối tượng thuộc lớp đó. ToBeInstanceOf đạt khi đối tượng là thể
    // hiện của lớp ApiError.
    expect(error).toBeInstanceOf(ApiError);
    // toBe(kỳ vọng): so bằng chặt (===), đỏ khi khác. Id trong lỗi phải đúng id ở header phản hồi
    // (để người dùng báo mã cho hỗ trợ).
    expect((error as ApiError).correlationId).toBe('test-cid-123');
  });

  /**
   * Kiểm tra: khi phản hồi lỗi không có header `X-Correlation-Id`, `ApiError.correlationId` là
   * null.
   * Lý do: tránh bịa hoặc để `undefined` khiến giao diện in ra mã sai.
   * Task nguồn: spec 016 (correlation ID xuyên suốt) — US2 AC2
   * (contracts/spa-correlation-visibility-contract.md).
   */
  it('is null when the failed response carries no correlation ID', async () => {
    server.use(
      http.get(`${GATEWAY_ORIGIN}/bff/products`, () =>
        HttpResponse.json({ title: 'Bad Gateway' }, { status: 502 }),
      ),
    );

    const error = await bffFetch('/bff/products').catch((caught: unknown) => caught);

    // toBeInstanceOf(lớp): xanh khi đối tượng thuộc lớp đó. Vẫn là ApiError.
    expect(error).toBeInstanceOf(ApiError);
    // toBeNull(): xanh khi giá trị là null. ToBeNull đạt khi null (không bịa id).
    expect((error as ApiError).correlationId).toBeNull();
  });
});

describe('bffFetch bearer token', () => {
  afterEach(() => configureAuthHooks({}));

  /**
   * Kiểm tra: khi có token, `bffFetch` gửi header `Authorization: Bearer <token>`.
   * Lý do: gateway deny-by-default (spec 014) nên thiếu header này thì mọi lời gọi bị 401.
   * Task nguồn: spec 004 (SPA mua sắm tối thiểu) — FR-026 (đăng nhập SPA, bearer token).
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

    // toBe(kỳ vọng): so bằng chặt (===), đỏ khi khác. Header phải đúng chuỗi "Bearer abc"; đỏ khi
    // thiếu (null) hoặc sai định dạng.
    expect(seen).toBe('Bearer abc');
  });

  /**
   * Kiểm tra: khi chưa đăng nhập, `bffFetch` không gửi header `Authorization`.
   * Lý do: không được gửi 'Bearer null' hay token rỗng lên gateway.
   * Task nguồn: spec 004 (SPA mua sắm tối thiểu) — FR-026 (đăng nhập SPA, bearer token).
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

    // toBeNull(): xanh khi giá trị là null. Không có header Authorization; đỏ khi gửi "Bearer
    // null".
    expect(seen).toBeNull();
  });

  /**
   * Kiểm tra: chỉ khi request đã mang token mà vẫn bị 401 thì `onUnauthorized` mới được gọi.
   * Lý do: 401 lúc chưa đăng nhập là bình thường (không được kích hoạt đăng xuất); 401 khi có token
   * nghĩa là token hết hạn/bị từ chối nên SPA phải đưa người dùng về đăng nhập.
   * Task nguồn: spec 004 (SPA mua sắm tối thiểu) — FR-026 (đăng nhập SPA, bearer token).
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
    // not.toHaveBeenCalled(): xanh khi hàm giả chưa được gọi lần nào. Sau lần không token, hàm giả
    // KHÔNG được gọi (401 lúc chưa đăng nhập là bình thường).
    expect(onUnauthorized).not.toHaveBeenCalled();

    configureAuthHooks({ getAccessToken: () => 'abc', onUnauthorized });
    await bffFetch('/bff/products').catch(() => undefined);
    // toHaveBeenCalledTimes(n): xanh khi hàm giả được gọi đúng n lần. Sau lần có token, được gọi
    // đúng 1 lần (token bị từ chối thì phải đăng xuất).
    expect(onUnauthorized).toHaveBeenCalledTimes(1);
  });
});
