// lib/auth.ts — Auth web: JWT (jose) + RBAC + refresh token xoay vòng.
// Tương thích định dạng password hash PBKDF2 của backend .NET:
//   pbkdf2$200000$<salt-hex>$<dk-hex>
import { SignJWT, jwtVerify } from 'jose';
import crypto from 'crypto';
import { getDb } from './db';

export const ROLE_RANK: Record<string, number> = {
  viewer: 0,
  sales: 1,
  manager: 2,
  admin: 3,
};

export const ROLE_LABELS: Record<string, string> = {
  viewer: 'Người xem',
  sales: 'Nhân viên kinh doanh',
  manager: 'Quản lý',
  admin: 'Quản trị viên',
};

export const PERMISSION_MATRIX: { key: string; label: string; roles: string[] }[] = [
  { key: 'orders.view', label: 'Xem danh sách & chi tiết đơn hàng', roles: ['viewer', 'sales', 'manager', 'admin'] },
  { key: 'masterdata.view', label: 'Tra cứu KH / hàng hoá / thuế suất', roles: ['viewer', 'sales', 'manager', 'admin'] },
  { key: 'audit.view', label: 'Xem giám sát tích hợp', roles: ['viewer', 'sales', 'manager', 'admin'] },
  { key: 'orders.create', label: 'Tạo đơn hàng', roles: ['sales', 'manager', 'admin'] },
  { key: 'orders.update', label: 'Sửa đơn hàng (ghi chú, ngày giao…)', roles: ['sales', 'manager', 'admin'] },
  { key: 'orders.cancel_close', label: 'Huỷ / đóng đơn hàng', roles: ['manager', 'admin'] },
  { key: 'masterdata.sync', label: 'Đồng bộ master data vào DB local', roles: ['manager', 'admin'] },
  { key: 'audit.retry', label: 'Thử lại thao tác tích hợp lỗi', roles: ['manager', 'admin'] },
  { key: 'users.manage', label: 'Quản lý người dùng & phân quyền', roles: ['admin'] },
  { key: 'settings.connection', label: 'Cài đặt kết nối Service Layer', roles: ['admin'] },
];

export interface AppUser {
  username: string;
  full_name: string;
  role: string;
  sales_person_code: number | null;
  active: boolean;
  company_db?: string;
}

const JWT_SECRET = process.env.JWT_SECRET || 'demo-secret-32-chars-minimum-xxxxx';
const JWT_EXPIRE_MINUTES = parseInt(process.env.JWT_EXPIRE_MINUTES || '480', 10);
const jwtKey = new TextEncoder().encode(JWT_SECRET);

// ---------------- Password (PBKDF2-HMAC-SHA256, tương thích .NET) ----------------
export function hashPassword(password: string): string {
  const salt = crypto.randomBytes(16);
  const dk = crypto.pbkdf2Sync(password, salt, 200_000, 32, 'sha256');
  return `pbkdf2$200000$${salt.toString('hex')}$${dk.toString('hex')}`;
}

export function verifyPassword(password: string, hashed: string): boolean {
  try {
    const parts = hashed.split('$');
    const iters = parseInt(parts[1], 10);
    const salt = Buffer.from(parts[2], 'hex');
    const expected = Buffer.from(parts[3], 'hex');
    const dk = crypto.pbkdf2Sync(password, salt, iters, expected.length, 'sha256');
    return crypto.timingSafeEqual(dk, expected);
  } catch {
    return false;
  }
}

// ---------------- JWT ----------------
export async function createToken(
  username: string,
  role: string,
  companyDb = ''
): Promise<string> {
  // jose v6: claim tùy chỉnh (b1_db) đặt ngay trong payload constructor
  const payload: Record<string, unknown> = { sub: username, role };
  if (companyDb) payload.b1_db = companyDb;
  return new SignJWT(payload)
    .setProtectedHeader({ alg: 'HS256' })
    .setExpirationTime(`${JWT_EXPIRE_MINUTES}m`)
    .sign(jwtKey);
}

export interface JwtPayload {
  sub: string;
  role: string;
  b1_db?: string;
}

export async function verifyToken(token: string): Promise<JwtPayload | null> {
  try {
    const { payload } = await jwtVerify(token, jwtKey);
    return payload as unknown as JwtPayload;
  } catch {
    return null;
  }
}

// ---------------- Refresh token (xoay vòng, hạn 7 ngày) ----------------
function sha256(s: string): string {
  return crypto.createHash('sha256').update(s, 'utf8').digest('hex');
}

export function issueRefreshToken(username: string, companyDb = ''): string {
  const raw = crypto.randomBytes(48).toString('base64');
  const hash = sha256(raw);
  const db = getDb();
  db.prepare(
    `INSERT INTO refresh_tokens (token_hash, username, company_db, expires_at)
     VALUES (?, ?, ?, datetime('now', '+7 days'))`
  ).run(hash, username, companyDb || '');
  // Dọn token cũ của user (giữ tối đa 5)
  db.prepare(
    `DELETE FROM refresh_tokens WHERE username = ? AND id NOT IN (
       SELECT id FROM refresh_tokens WHERE username = ? ORDER BY id DESC LIMIT 5)`
  ).run(username, username);
  return raw;
}

export function consumeRefreshToken(rawToken: string): { username: string; companyDb: string } | null {
  const hash = sha256(rawToken || '');
  const db = getDb();
  const row = db
    .prepare(
      `SELECT username, company_db FROM refresh_tokens
       WHERE token_hash = ? AND revoked = 0 AND datetime(expires_at) > datetime('now')`
    )
    .get(hash) as { username: string; company_db: string } | undefined;
  if (!row) return null;
  // Thu hồi token vừa dùng (xoay vòng)
  db.prepare('UPDATE refresh_tokens SET revoked = 1 WHERE token_hash = ?').run(hash);
  const user = getUser(row.username);
  if (!user || !user.active) return null;
  return { username: row.username, companyDb: row.company_db || '' };
}

export function revokeRefreshToken(rawToken: string) {
  const db = getDb();
  db.prepare('UPDATE refresh_tokens SET revoked = 1 WHERE token_hash = ?').run(
    sha256(rawToken || '')
  );
}

// ---------------- Users ----------------
export function getUser(username: string): AppUser | null {
  const db = getDb();
  const r = db
    .prepare(
      'SELECT username, full_name, role, sales_person_code, active FROM users WHERE username = ?'
    )
    .get(username) as
    | {
        username: string;
        full_name: string;
        role: string;
        sales_person_code: number | null;
        active: number;
      }
    | undefined;
  if (!r) return null;
  return {
    username: r.username,
    full_name: r.full_name || '',
    role: r.role,
    sales_person_code: r.sales_person_code,
    active: r.active === 1,
  };
}

export function authenticate(username: string, password: string): AppUser | null {
  const user = getUser(username);
  if (!user || !user.active) return null;
  const db = getDb();
  const row = db
    .prepare('SELECT password_hash FROM users WHERE username = ?')
    .get(username) as { password_hash: string } | undefined;
  if (!row || !verifyPassword(password, row.password_hash)) return null;
  return user;
}

export function listUsers(): AppUser[] {
  const db = getDb();
  const rows = db
    .prepare(
      'SELECT username, full_name, role, sales_person_code, active FROM users ORDER BY username'
    )
    .all() as {
    username: string;
    full_name: string;
    role: string;
    sales_person_code: number | null;
    active: number;
  }[];
  return rows.map((r) => ({
    username: r.username,
    full_name: r.full_name || '',
    role: r.role,
    sales_person_code: r.sales_person_code,
    active: r.active === 1,
  }));
}

export function toUserOut(u: AppUser) {
  return {
    username: u.username,
    full_name: u.full_name,
    role: u.role,
    sales_person_code: u.sales_person_code,
    active: u.active,
    company_db: u.company_db || '',
  };
}

// ---------------- Auth guard cho route handlers ----------------
export class ApiError extends Error {
  status: number;
  constructor(status: number, message: string) {
    super(message);
    this.status = status;
  }
}

// Đọc user từ Authorization header; ném ApiError(401/403) nếu không đạt.
// companyDb: claim b1_db, fallback DB mặc định.
export async function requireAuth(req: Request, ...roles: string[]): Promise<AppUser> {
  const auth = req.headers.get('authorization') || '';
  const m = auth.match(/^Bearer\s+(.+)$/i);
  if (!m) throw new ApiError(401, 'Thiếu token đăng nhập');
  const payload = await verifyToken(m[1].trim());
  if (!payload?.sub) throw new ApiError(401, 'Token không hợp lệ hoặc đã hết hạn');
  const user = getUser(payload.sub);
  if (!user || !user.active) throw new ApiError(401, 'Token không hợp lệ hoặc đã hết hạn');
  if (roles.length > 0) {
    const minRank = Math.min(...roles.map((r) => ROLE_RANK[r] ?? 99));
    const rank = ROLE_RANK[user.role] ?? -1;
    if (rank < minRank) throw new ApiError(403, 'Không có quyền thực hiện thao tác này');
  }
  user.company_db = (payload.b1_db || '').trim() || defaultCompanyDb();
  return user;
}

export function defaultCompanyDb(): string {
  const db = getDb();
  const row = db
    .prepare("SELECT value FROM app_settings WHERE key = 'b1_company_db'")
    .get() as { value: string } | undefined;
  return row?.value || process.env.B1_COMPANY_DB || 'SBODEMO_VN';
}

export function getDatabases(): { code: string; name: string }[] {
  const db = getDb();
  const row = db
    .prepare("SELECT value FROM app_settings WHERE key = 'b1_databases'")
    .get() as { value: string } | undefined;
  if (row?.value) {
    try {
      const arr = JSON.parse(row.value) as { code: string; name: string }[];
      if (Array.isArray(arr) && arr.length) return arr;
    } catch {
      /* bỏ qua, dùng mặc định */
    }
  }
  const def = defaultCompanyDb();
  return [{ code: def, name: 'Demo VN' }];
}

export function getDiscountLimits(): { sales: number; manager: number } {
  const db = getDb();
  const s = db.prepare("SELECT value FROM app_settings WHERE key = 'discount_sales'").get() as
    | { value: string }
    | undefined;
  const mgt = db.prepare("SELECT value FROM app_settings WHERE key = 'discount_manager'").get() as
    | { value: string }
    | undefined;
  return {
    sales: s ? parseFloat(s.value) || 10 : 10,
    manager: mgt ? parseFloat(mgt.value) || 20 : 20,
  };
}
