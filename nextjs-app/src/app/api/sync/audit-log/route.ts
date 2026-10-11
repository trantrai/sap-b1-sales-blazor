// GET /api/sync/audit-log — Nhật ký thao tác Service Layer (màn Giám sát tích hợp).
import { handle, ok, auth } from '@/lib/http';
import { getDb } from '@/lib/db';


export async function GET(req: Request) {
  return handle(async () => {
    await auth(req, 'viewer');
    const q = new URL(req.url).searchParams;
    const page = Math.max(1, parseInt(q.get('page') || '1', 10));
    const pageSize = Math.min(100, parseInt(q.get('page_size') || '15', 10));
    const db = getDb();
    const total = (
      db.prepare('SELECT COUNT(*) as c FROM sl_audit').get() as { c: number }
    ).c;
    const rows = db
      .prepare('SELECT * FROM sl_audit ORDER BY id DESC LIMIT ? OFFSET ?')
      .all(pageSize, (page - 1) * pageSize) as Record<string, unknown>[];
    // Map sang dạng frontend Monitor.jsx đang dùng
    const items = rows.map((r) => ({
      id: r['id'],
      timestamp: r['created_at'],
      created_at: r['created_at'],
      action: r['operation'],
      doc_num: r['entity_key'],
      external_id: null,
      username: r['username'],
      status: Number(r['success']) === 1 ? 'success' : 'failed',
      duration_ms: r['duration_ms'],
      error: r['error'],
      entity_key: r['entity_key'],
    }));
    return ok({ total, items });
  });
}
