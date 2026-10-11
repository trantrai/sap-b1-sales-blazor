// POST /api/auth/logout — Thu hồi refresh token.
import { revokeRefreshToken } from '@/lib/auth';
import { handle, ok } from '@/lib/http';


export async function POST(req: Request) {
  return handle(async () => {
    const body = (await req.json().catch(() => ({}))) as { refresh_token?: string };
    if (body.refresh_token) revokeRefreshToken(body.refresh_token);
    return ok({ ok: true });
  });
}
