// lib/http.ts — Helpers dùng chung cho API route handlers.
import { NextResponse } from 'next/server';
import { ApiError, requireAuth, AppUser } from './auth';
import { logAction } from './db';

// Chuẩn hoá mọi route handler: bắt ApiError -> JSON {detail} + status.
export function handle<T>(fn: () => Promise<T> | T) {
  return (async () => {
    try {
      return await fn();
    } catch (e) {
      if (e instanceof ApiError)
        return NextResponse.json({ detail: e.message }, { status: e.status });
      const err = e as { status?: number; message?: string };
      const status = typeof err?.status === 'number' ? err.status : 500;
      const message = err?.message || 'Lỗi hệ thống';
      return NextResponse.json({ detail: message }, { status });
    }
  })();
}

export const ok = <T>(data: T, status = 200) => NextResponse.json(data, { status });

// Lấy IP client (cho audit log)
export function clientIp(req: Request): string {
  const h = req.headers.get('x-forwarded-for');
  if (h) return h.split(',')[0].trim();
  return req.headers.get('x-real-ip') || '';
}

// Guard phân quyền + trả về user
export async function auth(req: Request, ...roles: string[]): Promise<AppUser> {
  return requireAuth(req, ...roles);
}

// Ghi audit log thao tác web
export function audit(
  req: Request,
  user: AppUser,
  action: string,
  entity = 'Orders',
  entityKey: string | null = null,
  detail: Record<string, unknown> | null = null
) {
  try {
    logAction(user.username, action, entity, entityKey, detail, clientIp(req) || null);
  } catch {
    /* audit không được làm fail request */
  }
}
