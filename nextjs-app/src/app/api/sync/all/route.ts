// POST /api/sync/all — Đồng bộ master data mock vào DB local.
import { handle, ok, auth, audit } from '@/lib/http';
import { syncMockMasterData } from '@/lib/b1-client';


export async function POST(req: Request) {
  return handle(async () => {
    const user = await auth(req, 'manager');
    const companyDb = user.company_db || '';
    const counts = syncMockMasterData(companyDb);
    audit(req, user, 'sync.all', 'Sync', companyDb, counts);
    return ok({ ok: true, counts });
  });
}
