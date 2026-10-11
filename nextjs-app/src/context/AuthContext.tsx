// context/AuthContext.tsx — Quản lý phiên đăng nhập phía client.
'use client';

import { createContext, useContext, useState, useEffect, ReactNode } from 'react';
import { useRouter } from 'next/navigation';
import api from '@/lib/api-client';

export interface User {
  username: string;
  role: string;
  companyDb?: string;
}

interface AuthCtx {
  user: User | null;
  loading: boolean;
  login: (username: string, password: string, companyDb: string) => Promise<void>;
  logout: () => Promise<void>;
}

const Ctx = createContext<AuthCtx | null>(null);

function parseUser(token: string): User | null {
  try {
    const payload = JSON.parse(atob(token.split('.')[1]));
    return { username: payload.sub, role: payload.role, companyDb: payload.b1_db };
  } catch {
    return null;
  }
}

export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<User | null>(null);
  const [loading, setLoading] = useState(true);
  const router = useRouter();

  useEffect(() => {
    const token = localStorage.getItem('access_token');
    if (token) {
      const u = parseUser(token);
      if (u) setUser(u);
      else localStorage.clear();
    }
    setLoading(false);
  }, []);

  const login = async (username: string, password: string, companyDb: string) => {
    const { data } = await api.post<{ access_token: string; refresh_token: string }>(
      '/auth/login',
      { username, password, company_db: companyDb }
    );
    localStorage.setItem('access_token', data.access_token);
    localStorage.setItem('refresh_token', data.refresh_token);
    const u = parseUser(data.access_token);
    if (u) setUser(u);
  };

  const logout = async () => {
    try {
      await api.post('/auth/logout');
    } catch {
      /* bỏ qua */
    }
    localStorage.clear();
    setUser(null);
    router.push('/login');
  };

  return <Ctx.Provider value={{ user, loading, login, logout }}>{children}</Ctx.Provider>;
}

export const useAuth = (): AuthCtx => {
  const ctx = useContext(Ctx);
  if (!ctx) throw new Error('useAuth phải dùng trong AuthProvider');
  return ctx;
};
