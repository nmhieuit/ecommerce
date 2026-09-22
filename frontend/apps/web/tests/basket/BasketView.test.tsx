import { render, screen, within } from '@testing-library/react';
import { QueryClientProvider } from '@tanstack/react-query';
import { http, HttpResponse } from 'msw';
import type { ReactNode } from 'react';
import { beforeAll, describe, expect, it } from 'vitest';
import { configureApiClient } from '@ecommerce/api-client';
import { createQueryClient } from '@/app/queryClient';
import { BasketView } from '@/features/basket/BasketView';
import { server } from '../msw/server';

/**
 * Spec US2 acceptance scenario 3 and FR-004: "each item's name, quantity, and price are shown,
 * along with a basket total."
 */

const GATEWAY_ORIGIN = 'http://gateway.test';

beforeAll(() => {
  configureApiClient({ baseUrl: GATEWAY_ORIGIN });
});

function renderWithQueryClient(ui: ReactNode) {
  return render(<QueryClientProvider client={createQueryClient()}>{ui}</QueryClientProvider>);
}

interface BasketBody {
  readonly id: string;
  readonly customerRef: string;
  readonly items: readonly {
    readonly productId: string;
    readonly name: string;
    readonly quantity: number;
    readonly unitPrice: number;
    readonly lineTotal: number;
  }[];
  readonly total: number;
}

function respondWithBasket(body: BasketBody) {
  server.use(http.get(`${GATEWAY_ORIGIN}/bff/basket`, () => HttpResponse.json(body)));
}

const notebookLine = {
  productId: '9f8d6b1e-0001-4000-8000-000000000001',
  name: 'Field Notes Notebook',
  quantity: 2,
  unitPrice: 12.5,
  lineTotal: 25,
};

const apronLine = {
  productId: '9f8d6b1e-0001-4000-8000-000000000003',
  name: 'Linen Apron',
  quantity: 1,
  unitPrice: 34.25,
  lineTotal: 34.25,
};

describe('BasketView', () => {
  /**
   * Kiểm tra: mỗi dòng giỏ hiển thị tên, số lượng, đơn giá và thành tiền.
   * Lý do: US2 kịch bản 3/FR-004: giỏ phải hiện tên, số lượng, giá từng mặt hàng; assert theo role
   * trong phạm vi từng dòng để không bị mơ hồ khi thành tiền dòng bằng tổng giỏ.
   * Task nguồn: spec 004 (SPA mua sắm tối thiểu) — T036, US2 (FR-004).
   */
  it('shows each line with its name, quantity, unit price, and line total', async () => {
    respondWithBasket({
      id: 'b1b1b1b1-0000-4000-8000-000000000001',
      customerRef: 'phase1-stub-user',
      items: [notebookLine],
      total: 25,
    });

    renderWithQueryClient(<BasketView />);

    // toBeInTheDocument(): xanh khi phần tử có trong DOM. Chờ tên sản phẩm xuất hiện.
    expect(await screen.findByText('Field Notes Notebook')).toBeInTheDocument();

    // Scoped to the line, because a one-line basket's line total and basket total are the same
    // amount — an unscoped match would be ambiguous, and would still pass if the line total
    // vanished entirely.
    const line = within(screen.getByRole('listitem'));

    // Quantity and unit price read as one phrase — "Quantity: 2 × $12.50" — so they are asserted
    // as one, the way the shopper encounters them.
    // toBeInTheDocument(): xanh khi phần tử có trong DOM. Trong dòng hàng phải có chữ "Quantity: 2
    // × $12.50" (regex chấp nhận khoảng trắng tuỳ ý); đỏ khi định dạng số lượng/đơn giá sai.
    expect(line.getByText(/quantity:\s*2\s*×\s*\$12\.50/i)).toBeInTheDocument();
    // toBeInTheDocument(): xanh khi phần tử có trong DOM. Thành tiền của dòng.
    expect(line.getByText('$25.00')).toBeInTheDocument();
  });

  /**
   * Kiểm tra: hiển thị đúng tổng giỏ do backend báo ($59.25).
   * Lý do: tổng đến từ backend và chỉ được hiển thị, không tính lại ở trình duyệt — 1 tổng client
   * tự tính có thể lệch với số tiền bị tính thật (quickstart Scenario 2, 5).
   * Task nguồn: spec 004 (SPA mua sắm tối thiểu) — T036, US2 (FR-004).
   */
  it('shows the basket total the backend reported', async () => {
    respondWithBasket({
      id: 'b1b1b1b1-0000-4000-8000-000000000001',
      customerRef: 'phase1-stub-user',
      items: [notebookLine, apronLine],
      total: 59.25,
    });

    renderWithQueryClient(<BasketView />);

    // toBeInTheDocument(): xanh khi phần tử có trong DOM. Có nhãn Total.
    expect(await screen.findByText(/total/i)).toBeInTheDocument();
    // toBeInTheDocument(): xanh khi phần tử có trong DOM. Hiển thị đúng tổng backend báo (không tự
    // tính lại).
    expect(screen.getByText('$59.25')).toBeInTheDocument();
  });

  /**
   * Kiểm tra: các dòng giỏ được trình bày dưới dạng danh sách.
   * Lý do: trợ năng (FR-017): trình đọc màn hình đọc được số dòng trong giỏ.
   * Task nguồn: spec 004 (SPA mua sắm tối thiểu) — T036, US2 (FR-017).
   */
  it('presents the lines as a list', async () => {
    respondWithBasket({
      id: 'b1b1b1b1-0000-4000-8000-000000000001',
      customerRef: 'phase1-stub-user',
      items: [notebookLine, apronLine],
      total: 59.25,
    });

    renderWithQueryClient(<BasketView />);

    // toBeInTheDocument(): xanh khi phần tử có trong DOM. Có danh sách có tên chứa "basket" (hỗ trợ
    // trình đọc màn hình).
    expect(await screen.findByRole('list', { name: /basket/i })).toBeInTheDocument();
    // toHaveLength(n): xanh khi mảng/danh sách có đúng n phần tử. Có đúng 2 mục; đỏ khi thiếu/thừa.
    expect(screen.getAllByRole('listitem')).toHaveLength(2);
  });

  /**
   * Kiểm tra: giỏ rỗng hiện trạng thái trống dễ hiểu.
   * Lý do: giỏ rỗng là trạng thái hợp lệ — của người mua lần đầu và ngay sau khi thanh toán
   * (FR-010) — không phải lỗi.
   * Task nguồn: spec 004 (SPA mua sắm tối thiểu) — T036, US2/US3 (FR-010).
   */
  it('tells the shopper when the basket is empty', async () => {
    respondWithBasket({
      id: 'b1b1b1b1-0000-4000-8000-000000000001',
      customerRef: 'phase1-stub-user',
      items: [],
      total: 0,
    });

    renderWithQueryClient(<BasketView />);

    // toBeInTheDocument(): xanh khi phần tử có trong DOM. Có thông báo giỏ trống.
    expect(await screen.findByText(/your basket is empty/i)).toBeInTheDocument();
    // not.toBeInTheDocument(): xanh khi phần tử KHÔNG có trong DOM (queryBy trả null nếu không
    // thấy). .not đảo điều kiện: đạt khi KHÔNG có phần tử role="alert". Đỏ khi có thông báo lỗi.
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
  });

  /**
   * Kiểm tra: không tải được giỏ thì hiện lỗi đọc được.
   * Lý do: FR-012: mọi request tới backend thất bại đều phải cho người mua thấy thông báo rõ ràng.
   * Task nguồn: spec 004 (SPA mua sắm tối thiểu) — T036, US2 (FR-012).
   */
  it('shows a readable error when the basket cannot be loaded', async () => {
    server.use(
      http.get(`${GATEWAY_ORIGIN}/bff/basket`, () =>
        HttpResponse.json({ title: 'Bad Gateway' }, { status: 502 }),
      ),
    );

    renderWithQueryClient(<BasketView />);

    // toBeInTheDocument(): xanh khi phần tử có trong DOM. Chờ tối đa 5 giây cho cảnh báo lỗi.
    expect(await screen.findByRole('alert', {}, { timeout: 5000 })).toBeInTheDocument();
    // toBeInTheDocument(): xanh khi phần tử có trong DOM. Có nút "Try again"; getBy ném lỗi ngay
    // nếu không thấy.
    expect(screen.getByRole('button', { name: /try again/i })).toBeInTheDocument();
  });

  /**
   * Kiểm tra: dòng giỏ có thêm trường client chưa biết (vd. ngày backorder) vẫn hiển thị giỏ đầy
   * đủ.
   * Lý do: FR-006/SC-004 (tolerant reader): 1 dòng được backend làm giàu thêm không được làm người
   * mua mất giỏ; mock `server.use` trực tiếp vì cần 1 body mà kiểu `BasketBody` đã khai không mô
   * tả.
   * Task nguồn: spec 007 (hợp đồng OpenAPI cho BFF) — T011, US3 (FR-006, SC-004).
   */
  it('renders the basket when a line carries a field the client does not know about', async () => {
    server.use(
      http.get(`${GATEWAY_ORIGIN}/bff/basket`, () =>
        HttpResponse.json({
          id: 'b1b1b1b1-0000-4000-8000-000000000001',
          customerRef: 'phase1-stub-user',
          items: [{ ...notebookLine, backorderedUntilUtc: '2026-09-01T00:00:00Z' }, apronLine],
          total: 59.25,
        }),
      ),
    );

    renderWithQueryClient(<BasketView />);

    // toBeInTheDocument(): xanh khi phần tử có trong DOM.
    expect(await screen.findByText('Field Notes Notebook')).toBeInTheDocument();
    expect(screen.getByText('Linen Apron')).toBeInTheDocument();

    // Unambiguous without scoping: no other figure on screen is $25.00 (the enriched line's total)
    // or $59.25 (the basket's).
    expect(screen.getByText(/quantity:\s*2\s*×\s*\$12\.50/i)).toBeInTheDocument();
    expect(screen.getByText('$25.00')).toBeInTheDocument();
    expect(screen.getByText('$59.25')).toBeInTheDocument();
    // not.toBeInTheDocument(): xanh khi phần tử KHÔNG có trong DOM (queryBy trả null nếu không
    // thấy).
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
  });
});
