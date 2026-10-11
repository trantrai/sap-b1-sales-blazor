// POST /api/auth/login — Đăng nhập, trả về JWT + refresh token.
import { authenticate, createToken, issueRefreshToken, defaultCompanyDb, getDatabases } from '@/lib/auth';
import { ApiError } from '@/lib/auth';
import { handle, ok, audit } from '@/lib/http';
import { getDb } from '@/lib/db';


export async function POST(req: Request) {
  return handle(async () => {
    const body = (await req.json().catch(() => ({}))) as {
      username?: string;
      password?: string;
      company_db?: string;
    };
    const user = authenticate(body.username || '', body.password || '');
    if (!user) throw new ApiError(401, 'Sai tên đăng nhập hoặc mật khẩu');

    let companyDb = (body.company_db || '').trim();
    if (!companyDb) {
      companyDb = defaultCompanyDb();
    } else if (!getDatabases().some((d) => d.code.toLowerCase() === companyDb.toLowerCase())) {
      throw new ApiError(400, `Database '${companyDb}' chưa được khai báo trong hệ thống.`);
    }

    // Đảm bảo DB đã init (seed admin)
    getDb();
    audit(req, { ...user, company_db: companyDb }, 'auth.login', 'User', user.username, {
      company_db: companyDb,
    });

    const accessToken = await createToken(user.username, user.role, companyDb);
    const refreshToken = issueRefreshToken(user.username, companyDb);
    return ok({
      access_token: accessToken,
      token_type: 'bearer',
      refresh_token: refreshToken,
      expires_in: 480 * 60,
    });
  });
}
