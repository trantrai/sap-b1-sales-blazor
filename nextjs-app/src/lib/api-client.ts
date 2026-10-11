// lib/api-client.ts — HTTP client fetch-based, giao diện tương thích axios
// (api.get/post/put trả về {data}, lỗi có err.response.data.message).
// Chạy phía client ('use client').
const BASE = '/api';

interface ApiError extends Error {
  response?: { data?: { detail?: string; message?: string }; status?: number };
}

function makeError(status: number, data: unknown): ApiError {
  const err = new Error(
    (data as { detail?: string; message?: string })?.detail ||
      (data as { detail?: string; message?: string })?.message ||
      `Lỗi ${status}`
  ) as ApiError;
  err.response = { data: data as { detail?: string }, status };
  return err;
}

async function request<T>(
  method: string,
  url: string,
  data?: unknown,
  config?: { params?: Record<string, unknown>; responseType?: string }
): Promise<{ data: T }> {
  let full = BASE + url;
  if (config?.params) {
    const sp = new URLSearchParams();
    for (const [k, v] of Object.entries(config.params)) {
      if (v === undefined || v === null || v === '') continue;
      sp.set(k, String(v));
    }
    const qs = sp.toString();
    if (qs) full += `?${qs}`;
  }
  const token = typeof window !== 'undefined' ? localStorage.getItem('access_token') : null;
  const headers: Record<string, string> = {};
  if (data !== undefined) headers['Content-Type'] = 'application/json';
  if (token) headers['Authorization'] = `Bearer ${token}`;

  let res = await fetch(full, {
    method,
    headers,
    body: data !== undefined ? JSON.stringify(data) : undefined,
  });

  // Tự refresh token khi 401 (1 lần)
  if (res.status === 401 && typeof window !== 'undefined') {
    const refreshToken = localStorage.getItem('refresh_token');
    if (refreshToken) {
      try {
        const rr = await fetch(`${BASE}/auth/refresh`, {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({ refresh_token: refreshToken }),
        });
        if (rr.ok) {
          const rd = (await rr.json()) as { access_token: string; refresh_token: string };
          localStorage.setItem('access_token', rd.access_token);
          localStorage.setItem('refresh_token', rd.refresh_token);
          headers['Authorization'] = `Bearer ${rd.access_token}`;
          res = await fetch(full, {
            method,
            headers,
            body: data !== undefined ? JSON.stringify(data) : undefined,
          });
        } else {
          throw new Error('refresh failed');
        }
      } catch {
        localStorage.clear();
        window.location.href = '/login';
        throw makeError(401, { detail: 'Phiên đăng nhập hết hạn' });
      }
    } else {
      localStorage.clear();
      window.location.href = '/login';
      throw makeError(401, { detail: 'Phiên đăng nhập hết hạn' });
    }
  }

  if (!res.ok) {
    const body = await res.json().catch(() => ({}));
    throw makeError(res.status, body);
  }
  if (config?.responseType === 'blob') {
    const blob = await res.blob();
    return { data: blob as unknown as T };
  }
  const json = (await res.json().catch(() => ({}))) as T;
  return { data: json };
}

export const api = {
  get: <T>(url: string, config?: { params?: Record<string, unknown>; responseType?: string }) =>
    request<T>('GET', url, undefined, config),
  post: <T>(url: string, data?: unknown, config?: { params?: Record<string, unknown> }) =>
    request<T>('POST', url, data, config),
  put: <T>(url: string, data?: unknown) => request<T>('PUT', url, data),
  delete: <T>(url: string) => request<T>('DELETE', url),
};

export default api;
