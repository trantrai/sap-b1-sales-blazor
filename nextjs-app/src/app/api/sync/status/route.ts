// GET /api/sync/status — Trạng thái đồng bộ master data.
import { handle, ok, auth } from '@/lib/http';
import { getDb } from '@/lib/db';


export async function GET(req: Request) {
  return handle(async () => {
    const user = await auth(req, 'viewer');
    const companyDb = user.company_db || '';
    const db = getDb();
    const rows = db
      .prepare(
        `SELECT entity, COUNT(*) as cnt FROM md_sync_log WHERE company_db = ? GROUP BY entity`
      )
      .all(companyDb) as { entity: string; cnt: number }[];
    const lastRows = db
      .prepare(
        `SELECT entity, MAX(finished_at) as last_sync, SUM(records) as last_records
         FROM md_sync_log WHERE company_db = ? GROUP BY entity`
      )
      .all(companyDb) as { entity: string; last_sync: string | null; last_records: number }[];
    const byEntity = new Map(lastRows.map((r) => [r.entity, r]));
    const tables = rows.map((r) => ({
      table: r.entity,
      count: r.cnt,
      last_sync: byEntity.get(r.entity)?.last_sync || null,
      last_records: byEntity.get(r.entity)?.last_records || 0,
    }));
    // Map sang dạng frontend Sync.jsx đang dùng: status[entity] = {last_sync, count}
    const status: Record<string, { last_sync: string | null; count: number }> = {};
    for (const t of tables) status[t.table] = { last_sync: t.last_sync, count: t.count };
    return ok(status);
  });
}
