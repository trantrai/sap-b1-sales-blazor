// GET /api/orders/stats — Thống kê dashboard.
import { handle, ok, auth } from '@/lib/http';
import { getDashboardStats } from '@/lib/b1-client';
import { ApiError } from '@/lib/auth';


export async function GET(req: Request) {
  return handle(async () => {
    const user = await auth(req, 'viewer');
    let spc: number | null = null;
    if (user.role === 'sales') {
      if (!user.sales_person_code)
        throw new ApiError(403, 'Tài khoản chưa được gán nhân viên kinh doanh. Liên hệ quản trị viên.');
      spc = user.sales_person_code;
    }
    return ok(getDashboardStats(spc));
  });
}
