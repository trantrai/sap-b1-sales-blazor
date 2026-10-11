// POST /api/sync/retry-failed — Thử lại các thao tác SL thất bại (mock: đánh dấu đã xử lý).
import { handle, ok, auth, audit } from '@/lib/http';
import { getDb } from '@/lib/db';


export async function POST(req: Request) {
  return handle(async () => {
    const user = await auth(req, 'manager');
    const db = getDb();
    const failed = db
      .prepare('SELECT id FROM sl_audit WHERE success = 0')
      .all() as { id: number }[];
    // Mock mode: không có B1 thật để thử lại — đánh dấu note để theo dõi
    if (failed.length) {
      const mark = db.prepare("UPDATE sl_audit SET note = 'Đã thử lại (mock)' WHERE success = 0");
      mark.run();
    }
    audit(req, user, 'audit.retry_all', 'Sync', null, { retried: failed.length });
    return ok({ ok: true, retried: failed.length });
  });
}
