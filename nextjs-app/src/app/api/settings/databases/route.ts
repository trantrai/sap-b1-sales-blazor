// /api/settings/databases — GET/PUT danh sách database (chỉ admin).
import { handle, ok, auth, audit } from '@/lib/http';
import { getSetting, setSetting, getDb } from '@/lib/db';
import { defaultCompanyDb } from '@/lib/auth';


export async function GET(req: Request) {
  return handle(async () => {
    await auth(req, 'admin');
    getDb();
    const raw = getSetting('b1_databases');
    let databases: { code: string; name: string }[] = [];
    if (raw) {
      try {
        databases = JSON.parse(raw);
      } catch {
        databases = [];
      }
    }
    if (!databases.length) {
      const def = defaultCompanyDb();
      databases = [{ code: def, name: 'Demo VN' }];
    }
    return ok({ databases });
  });
}

export async function PUT(req: Request) {
  return handle(async () => {
    const user = await auth(req, 'admin');
    const body = (await req.json().catch(() => ({}))) as {
      databases?: { code: string; name: string }[];
    };
    const databases = (body.databases || [])
      .filter((d) => d.code?.trim())
      .map((d) => ({ code: d.code.trim().toUpperCase(), name: d.name?.trim() || d.code.trim() }));
    setSetting('b1_databases', JSON.stringify(databases));
    audit(req, user, 'settings.databases', 'Settings', 'databases', { count: databases.length });
    return ok({ ok: true, databases });
  });
}
