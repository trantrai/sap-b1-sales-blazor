// POST /api/settings/test-connection — Kiểm tra kết nối B1 (chỉ admin).
import { handle, ok, auth } from '@/lib/http';
import { getSetting } from '@/lib/db';


export async function POST(req: Request) {
  return handle(async () => {
    await auth(req, 'admin');
    const isMock = (getSetting('b1_mock') ?? 'true') === 'true';
    if (isMock) {
      return ok({
        success: true,
        message: 'Chế độ demo (dữ liệu giả) — không cần kết nối B1 thật.',
      });
    }
    // Mock mode tắt: thử ping URL Service Layer (không login thật)
    const url = getSetting('b1_service_layer_url') || '';
    try {
      const ctrl = new AbortController();
      const t = setTimeout(() => ctrl.abort(), 8000);
      await fetch(url, { method: 'HEAD', signal: ctrl.signal }).catch(() => null);
      clearTimeout(t);
      return ok({ success: true, message: `Đã gửi yêu cầu tới ${url}.` });
    } catch {
      return ok({ success: false, message: `Không kết nối được tới ${url}.` });
    }
  });
}
