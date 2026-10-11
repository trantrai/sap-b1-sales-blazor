// GET /api/masterdata/customers?search= — Tra cứu khách hàng.
import { handle, ok, auth } from '@/lib/http';
import { searchCustomers } from '@/lib/b1-client';


export async function GET(req: Request) {
  return handle(async () => {
    await auth(req, 'viewer');
    const q = new URL(req.url).searchParams.get('search') || '';
    return ok(searchCustomers(q));
  });
}
