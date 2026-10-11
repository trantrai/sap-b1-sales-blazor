// GET /api/audit — Nhật ký thao tác người dùng (màn Audit).
import { handle, ok, auth } from '@/lib/http';
import { getDb } from '@/lib/db';


export async function GET(req: Request) {
  return handle(async () => {
    await auth(req, 'viewer');
    const q = new URL(req.url).searchParams;
    const page = Math.max(1, parseInt(q.get('page') || '1', 10));
    const pageSize = Math.min(100, parseInt(q.get('page_size') || '20', 10));
    const db = getDb();
    const total = (
      db.prepare('SELECT COUNT(*) as c FROM audit_logs').get() as { c: number }
    ).c;
    const rows = db
      .prepare('SELECT * FROM audit_logs ORDER BY id DESC LIMIT ? OFFSET ?')
      .all(pageSize, (page - 1) * pageSize) as Record<string, unknown>[];
    // Map sang dạng frontend Audit.jsx đang dùng
    const items = rows.map((r) => ({
      id: r['id'],
      timestamp: r['created_at'],
      created_at: r['created_at'],
      username: r['username'],
      action: r['action'],
      entity_type: r['entity'],
      entity_id: r['entity_key'],
      ip: r['ip'],
      detail: r['detail'],
    }));
    return ok({ total, items });
  });
}
