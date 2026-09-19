import { render, screen } from '@testing-library/react';
import { QueryClientProvider } from '@tanstack/react-query';
import { http, HttpResponse } from 'msw';
import type { ReactNode } from 'react';
import { beforeAll, describe, expect, it } from 'vitest';
import { configureApiClient } from '@ecommerce/api-client';
import { createQueryClient } from '@/app/queryClient';
import { ProductList } from '@/features/catalog/ProductList';
import { server } from '../msw/server';

/**
 * Spec US1 acceptance scenario 1 and FR-001: the shopper opens the storefront and sees the products
 * available to buy, each with its name and price, retrieved from the backend rather than embedded
 * in the client.
 *
 * Assertions go through accessible roles rather than class names or test ids (Principle III), so
 * they describe what a shopper — including one using a screen reader — actually encounters.
 */

const GATEWAY_ORIGIN = 'http://gateway.test';

beforeAll(() => {
  configureApiClient({ baseUrl: GATEWAY_ORIGIN });
});

function renderWithQueryClient(ui: ReactNode) {
  return render(<QueryClientProvider client={createQueryClient()}>{ui}</QueryClientProvider>);
}

function respondWithProducts(items: unknown[]) {
  server.use(
    http.get(`${GATEWAY_ORIGIN}/bff/products`, () => HttpResponse.json({ items })),
  );
}

describe('ProductList', () => {
  /**
   * Kiểm tra: danh sách hiển thị đủ mọi sản phẩm với tên và giá đã định dạng (không phải số thô).
   * Lý do phải test: US1 kịch bản 1/FR-001: người mua mở storefront thấy sản phẩm lấy từ backend;
   * giá phải đọc được như tiền (FR-024) — "48" không phải 1 mức giá.
   * Task nguồn: spec 004 (SPA mua sắm tối thiểu) — T024, US1 (FR-001, FR-024).
   */
  it('lists every product with its name and price', async () => {
    respondWithProducts([
      { id: '9f8d6b1e-0001-4000-8000-000000000001', name: 'Field Notes Notebook', price: 12.5 },
      { id: '9f8d6b1e-0001-4000-8000-000000000002', name: 'Ceramic Pour-Over Set', price: 48 },
    ]);

    renderWithQueryClient(<ProductList />);

    expect(await screen.findByText('Field Notes Notebook')).toBeInTheDocument();
    expect(screen.getByText('Ceramic Pour-Over Set')).toBeInTheDocument();

    // Formatted, not raw: spec FR-024 is what the shopper reads, and "48" is not a price.
    expect(screen.getByText('$12.50')).toBeInTheDocument();
    expect(screen.getByText('$48.00')).toBeInTheDocument();
  });

  /**
   * Kiểm tra: catalog được trình bày dưới dạng danh sách (role `list`).
   * Lý do phải test: người dùng trình đọc màn hình nhờ đó biết có bao nhiêu sản phẩm trước khi nghe
   * từng mục (FR-017); assert theo role chứ không theo class hay test id.
   * Task nguồn: spec 004 (SPA mua sắm tối thiểu) — T024, US1 (FR-017).
   */
  it('presents the catalog as a list', async () => {
    respondWithProducts([
      { id: '9f8d6b1e-0001-4000-8000-000000000003', name: 'Linen Apron', price: 34.25 },
    ]);

    renderWithQueryClient(<ProductList />);

    expect(await screen.findByRole('list', { name: /products/i })).toBeInTheDocument();
    expect(screen.getAllByRole('listitem')).toHaveLength(1);
  });

  /**
   * Kiểm tra: catalog không có sản phẩm nào thì hiển thị trạng thái rỗng tường minh.
   * Lý do phải test: FR-002: 0 sản phẩm là câu trả lời hợp lệ và người mua phải được báo, thay vì
   * nhìn 1 trang trông như bị hỏng.
   * Task nguồn: spec 004 (SPA mua sắm tối thiểu) — T024/T025, US1 (FR-002).
   */
  it('shows the empty state when the catalog holds nothing', async () => {
    respondWithProducts([]);

    renderWithQueryClient(<ProductList />);

    expect(await screen.findByText(/no products available/i)).toBeInTheDocument();
    expect(screen.queryByRole('list', { name: /products/i })).not.toBeInTheDocument();
  });

  /**
   * Kiểm tra: backend lỗi thì hiện thông báo đọc được và trang vẫn dùng được.
   * Lý do phải test: FR-012/US1 kịch bản 3: không màn hình trắng, không vòng quay vô tận. Test giả
   * lập lỗi 500; trường hợp 401 (chưa đăng nhập/hết phiên) được xử lý riêng bằng việc đưa về form
   * đăng nhập — xem tests/auth/signIn.test.tsx.
   * Task nguồn: spec 004 (SPA mua sắm tối thiểu) — T024/T026, US1 (FR-012).
   */
  it('shows a readable error when the backend fails', async () => {
    server.use(
      http.get(`${GATEWAY_ORIGIN}/bff/products`, () =>
        HttpResponse.json({ title: 'Bad Gateway' }, { status: 502 }),
      ),
    );

    renderWithQueryClient(<ProductList />);

    const alert = await screen.findByRole('alert', {}, { timeout: 5000 });

    expect(alert).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /try again/i })).toBeInTheDocument();
  });

  /**
   * Kiểm tra: lời gọi lấy catalog đi tới đúng origin gateway đã cấu hình.
   * Lý do phải test: SC-010: mọi request của storefront đi tới 1 bề mặt backend duy nhất. Kiểm tra
   * ở mức đơn vị vì URL hard-code trong component nếu không sẽ chỉ bị e2e (T065) bắt.
   * Task nguồn: spec 004 (SPA mua sắm tối thiểu) — T024, US1 (FR-014, SC-010).
   */
  it('requests the catalog from the configured gateway origin', async () => {
    const requested: string[] = [];
    server.use(
      http.get(`${GATEWAY_ORIGIN}/bff/products`, ({ request }) => {
        requested.push(request.url);
        return HttpResponse.json({ items: [] });
      }),
    );

    renderWithQueryClient(<ProductList />);
    await screen.findByText(/no products available/i);

    expect(requested).toHaveLength(1);
    expect(requested[0]).toBe(`${GATEWAY_ORIGIN}/bff/products`);
  });

  /**
   * Kiểm tra: sản phẩm có thêm 1 trường client chưa biết (vd. `sku`) vẫn hiển thị bình thường.
   * Lý do phải test: Principle II: "consumer phải chịu được trường lạ". BFF có thể thêm trường và
   * deploy trước khi client được sinh lại; storefront không được trắng trang vì backend đi trước.
   * Task nguồn: bổ sung sau spec 004 (tolerant reader — Constitution Principle II); không thuộc
   * danh sách task T001-T071 của 004.
   */
  it('renders products carrying a field the client does not know about', async () => {
    respondWithProducts([
      {
        id: '9f8d6b1e-0001-4000-8000-000000000004',
        name: 'Enamel Camp Mug',
        price: 18,
        sku: 'MUG-ENAMEL-01',
      },
    ]);

    renderWithQueryClient(<ProductList />);

    expect(await screen.findByText('Enamel Camp Mug')).toBeInTheDocument();
    expect(screen.getByText('$18.00')).toBeInTheDocument();
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
  });
});
