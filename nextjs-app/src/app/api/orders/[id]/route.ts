// GET /api/orders/[id] — Chi tiết đơn hàng.
import { handle, ok, auth } from '@/lib/http';
import { getOrder } from '@/lib/b1-client';
import { ApiError, AppUser } from '@/lib/auth';


// NVKD chỉ được xem đơn của mình
function ensureOwnership(user: AppUser, order: { sales_person_code: number | null }) {
  if (user.role !== 'sales') return;
  if (!user.sales_person_code)
    throw new ApiError(403, 'Tài khoản chưa được gán nhân viên kinh doanh. Liên hệ quản trị viên.');
  if (order.sales_person_code !== user.sales_person_code)
    throw new ApiError(403, 'Bạn chỉ được thao tác đơn hàng của mình.');
}

export async function GET(req: Request, { params }: { params: Promise<{ id: string }> }) {
  return handle(async () => {
    const user = await auth(req, 'viewer');
    const { id } = await params;
    const docEntry = parseInt(id, 10);
    if (!Number.isFinite(docEntry)) throw new ApiError(404, 'Không tìm thấy đơn hàng.');
    const { order, etag } = getOrder(docEntry);
    ensureOwnership(user, order);
    return ok(order, 200);
  });
}
