// GET /api/auth/users — Danh sách người dùng (chỉ admin).
import { listUsers, toUserOut } from '@/lib/auth';
import { handle, ok, auth } from '@/lib/http';


export async function GET(req: Request) {
  return handle(async () => {
    await auth(req, 'admin');
    return ok(listUsers().map(toUserOut));
  });
}
