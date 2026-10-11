// GET /api/masterdata/items?search= — Tra cứu hàng hoá.
import { handle, ok, auth } from '@/lib/http';
import { searchItems } from '@/lib/b1-client';


export async function GET(req: Request) {
  return handle(async () => {
    await auth(req, 'viewer');
    const q = new URL(req.url).searchParams.get('search') || '';
    return ok(searchItems(q));
  });
}
