// POST /api/orders/[id]/close — Đóng đơn/dòng (manager+).
import { handle, ok, auth, audit } from '@/lib/http';
import { closeOrder } from '@/lib/b1-client';
import { ApiError } from '@/lib/auth';


export async function POST(req: Request, { params }: { params: Promise<{ id: string }> }) {
  return handle(async () => {
    const user = await auth(req, 'manager');
    const { id } = await params;
    const docEntry = parseInt(id, 10);
    if (!Number.isFinite(docEntry)) throw new ApiError(404, 'Không tìm thấy đơn hàng.');
    const body = (await req.json().catch(() => ({}))) as { line_nums?: number[] };
    const order = closeOrder(docEntry, body.line_nums ?? null, user.username);
    audit(req, user, 'order.close', 'Orders', String(docEntry), { line_nums: body.line_nums });
    return ok(order);
  });
}
