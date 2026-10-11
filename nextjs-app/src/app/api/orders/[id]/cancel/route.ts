// POST /api/orders/[id]/cancel — Huỷ đơn (manager+).
import { handle, ok, auth, audit } from '@/lib/http';
import { cancelOrder } from '@/lib/b1-client';
import { ApiError } from '@/lib/auth';


export async function POST(req: Request, { params }: { params: Promise<{ id: string }> }) {
  return handle(async () => {
    const user = await auth(req, 'manager');
    const { id } = await params;
    const docEntry = parseInt(id, 10);
    if (!Number.isFinite(docEntry)) throw new ApiError(404, 'Không tìm thấy đơn hàng.');
    const order = cancelOrder(docEntry, user.username);
    audit(req, user, 'order.cancel', 'Orders', String(docEntry), { doc_num: order.doc_num });
    return ok(order);
  });
}
