// POST /api/auth/refresh — Đổi refresh token lấy cặp token mới (xoay vòng).
import { consumeRefreshToken, createToken, issueRefreshToken, getUser, ApiError } from '@/lib/auth';
import { handle, ok, audit } from '@/lib/http';


export async function POST(req: Request) {
  return handle(async () => {
    const body = (await req.json().catch(() => ({}))) as { refresh_token?: string };
    const result = consumeRefreshToken(body.refresh_token || '');
    if (!result) throw new ApiError(401, 'Refresh token không hợp lệ hoặc đã hết hạn');
    const user = getUser(result.username);
    if (!user || !user.active) throw new ApiError(401, 'Tài khoản đã bị vô hiệu hoá');

    audit(req, { ...user, company_db: result.companyDb }, 'auth.refresh', 'User', user.username);
    return ok({
      access_token: await createToken(user.username, user.role, result.companyDb),
      token_type: 'bearer',
      refresh_token: issueRefreshToken(user.username, result.companyDb),
      expires_in: 480 * 60,
    });
  });
}
