// /api/orders — GET danh sách (phân trang), POST tạo đơn.
import { ApiError, getDiscountLimits, AppUser } from '@/lib/auth';
import { handle, ok, auth, audit } from '@/lib/http';
import { listOrders, createOrder, OrderCreateIn } from '@/lib/b1-client';


// NVKD bắt buộc có SalesPersonCode (fail-closed 403)
function requireSalesCode(user: AppUser): number {
  if (user.role === 'sales' && !user.sales_person_code)
    throw new ApiError(403, 'Tài khoản chưa được gán nhân viên kinh doanh. Liên hệ quản trị viên.');
  return user.sales_person_code ?? 0;
}

function validateCreate(body: OrderCreateIn) {
  if (!body.card_code?.trim()) throw new ApiError(400, 'Mã khách hàng không được để trống.');
  if (!body.lines || body.lines.length === 0)
    throw new ApiError(400, 'Đơn hàng phải có ít nhất một dòng hàng.');
  for (const l of body.lines) {
    if (!l.item_code?.trim()) throw new ApiError(400, 'Mã hàng hoá không được để trống.');
    if (!Number.isFinite(l.quantity) || l.quantity <= 0)
      throw new ApiError(400, `Số lượng phải > 0 (mã ${l.item_code}).`);
    if (!Number.isFinite(l.price) || l.price < 0)
      throw new ApiError(400, `Đơn giá không hợp lệ (mã ${l.item_code}).`);
  }
}

function validateDiscounts(user: AppUser, headerDisc: number, lineDiscs: number[]) {
  const all = [headerDisc, ...lineDiscs];
  if (all.some((d) => d < 0 || d > 100))
    throw new ApiError(400, 'Chiết khấu phải nằm trong khoảng 0–100%.');
  const limits = getDiscountLimits();
  const limit = user.role === 'admin' ? 100 : user.role === 'manager' ? limits.manager : limits.sales;
  const over = all.find((d) => d > limit);
  if (over !== undefined && over > limit)
    throw new ApiError(
      403,
      `Vai trò '${user.role}' chỉ được áp chiết khấu tối đa ${limit}% (đang nhập ${over}%).`
    );
}

export async function GET(req: Request) {
  return handle(async () => {
    const user = await auth(req, 'viewer');
    const url = new URL(req.url);
    const q = url.searchParams;
    const status = q.get('status') || '';
    const cardCode = q.get('card_code') || null;
    const docNumRaw = q.get('doc_num');
    const docNum = docNumRaw ? parseInt(docNumRaw, 10) : null;
    // Hỗ trợ cả page/page_size (frontend) và top/skip (tương thích .NET)
    let top = 50;
    let skip = 0;
    if (q.get('page')) {
      const page = Math.max(1, parseInt(q.get('page') || '1', 10));
      const pageSize = Math.min(200, Math.max(1, parseInt(q.get('page_size') || '20', 10)));
      top = pageSize;
      skip = (page - 1) * pageSize;
    } else {
      top = Math.min(200, parseInt(q.get('top') || '50', 10));
      skip = parseInt(q.get('skip') || '0', 10);
    }
    let spc: number | null = null;
    if (user.role === 'sales') spc = requireSalesCode(user);
    const result = listOrders(status, cardCode, docNum, spc, top, skip);
    return ok({ total: result.total, items: result.items });
  });
}

export async function POST(req: Request) {
  return handle(async () => {
    const user = await auth(req, 'sales');
    const body = (await req.json()) as OrderCreateIn;
    // Chuẩn hoá alias của frontend React: doc_discount→discount_percent, notes→comments, discount→discount_percent
    if (body.doc_discount !== undefined && body.discount_percent === undefined)
      body.discount_percent = body.doc_discount;
    if (body.notes !== undefined && body.comments === undefined) body.comments = body.notes;
    for (const l of body.lines || [])
      if (l.discount !== undefined && l.discount_percent === undefined)
        l.discount_percent = l.discount;
    validateCreate(body);
    // Sales luôn tạo đơn dưới mã của mình — ghi đè mọi giá trị client gửi lên
    if (user.role === 'sales') body.sales_person_code = requireSalesCode(user);
    validateDiscounts(
      user,
      body.discount_percent ?? 0,
      (body.lines || []).map((l) => l.discount_percent ?? 0)
    );
    const { order, created } = createOrder(body, user.username);
    audit(req, user, 'order.create', 'Orders', String(order.doc_entry), {
      doc_num: order.doc_num,
      card_code: order.card_code,
      total: order.doc_total,
      idempotent_hit: !created,
    });
    return ok(order, 201);
  });
}
