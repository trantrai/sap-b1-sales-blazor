// GET /api/auth/databases — Danh sách database (public, dùng ở màn Login).
import { getDatabases, defaultCompanyDb } from '@/lib/auth';
import { handle, ok } from '@/lib/http';
import { getDb } from '@/lib/db';


export async function GET() {
  return handle(async () => {
    getDb(); // đảm bảo schema đã init
    return ok({ databases: getDatabases(), default: defaultCompanyDb() });
  });
}
