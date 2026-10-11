// /api/settings — GET/PUT cài đặt kết nối (chỉ admin).
import { handle, ok, auth, audit } from '@/lib/http';
import { getSetting, setSetting, getDb } from '@/lib/db';


function readAll() {
  getDb();
  const g = (k: string, d: string) => getSetting(k) ?? d;
  return {
    b1_mode: g('b1_mock', 'true') === 'true' ? 'demo' : 'live',
    b1_mock: g('b1_mock', 'true') === 'true',
    b1_url: g('b1_service_layer_url', 'https://your-b1-server:50000/b1s/v1'),
    b1_company_db: g('b1_company_db', 'SBODEMO_VN'),
    b1_username: g('b1_username', 'manager'),
    b1_timeout: parseFloat(g('b1_timeout', '30')) || 30,
    b1_ssl_verify: g('b1_verify_ssl', 'true') === 'true',
  };
}

export async function GET(req: Request) {
  return handle(async () => {
    await auth(req, 'admin');
    return ok(readAll());
  });
}

export async function PUT(req: Request) {
  return handle(async () => {
    const user = await auth(req, 'admin');
    const body = (await req.json().catch(() => ({}))) as Record<string, unknown>;
    if (body.b1_mode !== undefined)
      setSetting('b1_mock', body.b1_mode === 'demo' ? 'true' : 'false');
    if (body.b1_url !== undefined) setSetting('b1_service_layer_url', String(body.b1_url));
    if (body.b1_company_db !== undefined) setSetting('b1_company_db', String(body.b1_company_db));
    if (body.b1_username !== undefined) setSetting('b1_username', String(body.b1_username));
    if (body.b1_password) setSetting('b1_password', String(body.b1_password));
    if (body.b1_timeout !== undefined) setSetting('b1_timeout', String(body.b1_timeout));
    if (body.b1_ssl_verify !== undefined)
      setSetting('b1_verify_ssl', body.b1_ssl_verify ? 'true' : 'false');
    audit(req, user, 'settings.connection', 'Settings', 'b1_connection', {
      b1_mode: body.b1_mode,
      url: body.b1_url,
    });
    return ok({ ok: true, ...readAll() });
  });
}
