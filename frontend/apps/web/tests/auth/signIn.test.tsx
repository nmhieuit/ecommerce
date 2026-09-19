import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { afterEach, beforeEach, describe, expect, it } from 'vitest';
import { App } from '@/App';
import { clearSession, getAccessToken, setSession } from '@/auth/tokenStore';
import { server } from '../msw/server';

/**
 * Sign-in gates the whole storefront: signed-out visitors land on the login form, a correct
 * credential opens the catalog with the bearer token attached, a wrong one says so, and signing out
 * (or the gateway refusing the token) closes the door again.
 */

const GATEWAY = 'http://localhost:5300';
const IDENTITY = 'http://localhost:5205';

function stubCatalog() {
  server.use(
    http.get(`${GATEWAY}/bff/products`, ({ request }) => {
      seenAuthorization.push(request.headers.get('Authorization'));
      return HttpResponse.json({ items: [] });
    }),
  );
}

let seenAuthorization: (string | null)[] = [];

beforeEach(() => {
  window.history.pushState({}, '', '/');
  seenAuthorization = [];
  clearSession();
});

afterEach(() => clearSession());

async function signInWith(username: string, password: string) {
  const user = userEvent.setup();
  await user.type(screen.getByLabelText('Username'), username);
  await user.type(screen.getByLabelText('Password'), password);
  await user.click(screen.getByRole('button', { name: 'Sign in' }));
}

describe('sign-in', () => {
  /**
   * Kiểm tra: người truy cập chưa đăng nhập mở `/` thì bị đưa tới form đăng nhập và không thấy
   * thanh điều hướng của cửa hàng.
   * Lý do phải test: FR-026: mọi màn hình trừ đăng nhập đều yêu cầu đã đăng nhập; gateway đã đòi
   * token cho mọi route nên storefront không thể hiển thị catalog cho khách vô danh.
   * Task nguồn: spec 004 (SPA mua sắm tối thiểu) — FR-026 (đăng nhập, bổ sung cùng đợt cutover danh
   * tính của spec 014).
   */
  it('sends a signed-out visitor to the login form', async () => {
    render(<App />);

    expect(await screen.findByRole('heading', { name: 'Sign in' })).toBeInTheDocument();
    expect(screen.queryByRole('navigation', { name: 'Main' })).not.toBeInTheDocument();
  });

  /**
   * Kiểm tra: đăng nhập đúng thì mở catalog và request tới gateway mang header `Authorization:
   * Bearer <token>`.
   * Lý do phải test: nhánh happy-case của FR-026/FR-015: token là danh tính của người mua trên mọi
   * request; tenant và subject vẫn do gateway suy ra từ claim của token, client không tự đặt.
   * Task nguồn: spec 004 (SPA mua sắm tối thiểu) — FR-026 (đăng nhập, bổ sung cùng đợt cutover danh
   * tính của spec 014).
   */
  it('opens the catalog with the bearer token after a correct sign-in', async () => {
    stubCatalog();
    server.use(
      http.post(`${IDENTITY}/connect/token`, async ({ request }) => {
        const form = new URLSearchParams(await request.text());
        expect(form.get('grant_type')).toBe('password');
        expect(form.get('client_id')).toBe('ecommerce-web-spa-password');
        expect(form.get('username')).toBe('shopper@test');

        return HttpResponse.json({ access_token: 'issued-token', expires_in: 3600 });
      }),
    );
    render(<App />);
    await screen.findByRole('heading', { name: 'Sign in' });

    await signInWith('shopper@test', 'correct-password');

    expect(
      await screen.findByRole('heading', { name: 'Products' }, { timeout: 5000 }),
    ).toBeInTheDocument();
    expect(getAccessToken()).toBe('issued-token');
    expect(seenAuthorization).toContain('Bearer issued-token');
  });

  /**
   * Kiểm tra: identity server từ chối (400 invalid_grant) thì form báo "Incorrect username or
   * password." và không lưu token.
   * Lý do phải test: người mua phải biết mình nhập sai; không để lại token nào trong bộ nhớ phiên.
   * Task nguồn: spec 004 (SPA mua sắm tối thiểu) — FR-026 (đăng nhập, bổ sung cùng đợt cutover danh
   * tính của spec 014).
   */
  it('says the credential is wrong when the identity server refuses it', async () => {
    server.use(
      http.post(`${IDENTITY}/connect/token`, () =>
        HttpResponse.json({ error: 'invalid_grant' }, { status: 400 }),
      ),
    );
    render(<App />);
    await screen.findByRole('heading', { name: 'Sign in' });

    await signInWith('shopper@test', 'wrong');

    expect(await screen.findByRole('alert')).toHaveTextContent('Incorrect username or password.');
    expect(getAccessToken()).toBeNull();
  });

  /**
   * Kiểm tra: identity server không truy cập được (lỗi mạng) thì form báo "Sign-in is unavailable",
   * khác thông báo sai mật khẩu.
   * Lý do phải test: phân biệt lỗi do người dùng với sự cố hệ thống, để người mua không đổi mật
   * khẩu vô ích khi hệ thống đang gián đoạn (FR-012).
   * Task nguồn: spec 004 (SPA mua sắm tối thiểu) — FR-026 (đăng nhập, bổ sung cùng đợt cutover danh
   * tính của spec 014).
   */
  it('does not call a wrong password an outage, nor an outage a wrong password', async () => {
    server.use(http.post(`${IDENTITY}/connect/token`, () => HttpResponse.error()));
    render(<App />);
    await screen.findByRole('heading', { name: 'Sign in' });

    await signInWith('shopper@test', 'anything');

    expect(await screen.findByRole('alert')).toHaveTextContent('Sign-in is unavailable');
  });

  /**
   * Kiểm tra: bấm "Sign out" thì quay về form đăng nhập và token bị xoá.
   * Lý do phải test: kết thúc phiên phải xoá thật bằng chứng danh tính khỏi trình duyệt, không chỉ
   * ẩn giao diện.
   * Task nguồn: spec 004 (SPA mua sắm tối thiểu) — FR-026 (đăng nhập, bổ sung cùng đợt cutover danh
   * tính của spec 014).
   */
  it('returns to the login form after Sign out', async () => {
    stubCatalog();
    setSession({ accessToken: 'test-token', expiresAt: Date.now() + 60_000 });
    render(<App />);
    await screen.findByRole('heading', { name: 'Products' });

    await userEvent.setup().click(screen.getByRole('button', { name: 'Sign out' }));

    expect(await screen.findByRole('heading', { name: 'Sign in' })).toBeInTheDocument();
    expect(getAccessToken()).toBeNull();
  });

  /**
   * Kiểm tra: gateway trả 401 cho request mang token (hết hạn/bị thu hồi/sai issuer) thì storefront
   * xoá phiên và quay về form đăng nhập.
   * Lý do phải test: phiên đã hết hiệu lực thì người mua phải được đưa lại tới đăng nhập, thay vì
   * kẹt ở màn hình "Loading…" mãi (lỗi từng quan sát khi chưa có đăng nhập: 401 mà không có thông
   * báo).
   * Task nguồn: spec 004 (SPA mua sắm tối thiểu) — FR-026 (đăng nhập, bổ sung cùng đợt cutover danh
   * tính của spec 014).
   */
  it('returns to the login form when the gateway refuses the token (401)', async () => {
    setSession({ accessToken: 'stale-token', expiresAt: Date.now() + 60_000 });
    server.use(
      http.get(`${GATEWAY}/bff/products`, () =>
        HttpResponse.json({ title: 'Unauthorized' }, { status: 401 }),
      ),
    );
    render(<App />);

    expect(await screen.findByRole('heading', { name: 'Sign in' })).toBeInTheDocument();
    expect(getAccessToken()).toBeNull();
  });
});
