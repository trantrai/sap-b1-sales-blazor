// app/login/page.tsx — Màn hình đăng nhập.
'use client';

import { useState, useEffect } from 'react';
import { useRouter } from 'next/navigation';
import { useAuth } from '@/context/AuthContext';
import api from '@/lib/api-client';

interface DbEntry {
  code: string;
  name: string;
}

export default function Login() {
  const [username, setUsername] = useState('');
  const [password, setPassword] = useState('');
  const [databases, setDatabases] = useState<DbEntry[]>([]);
  const [companyDb, setCompanyDb] = useState('');
  const [error, setError] = useState('');
  const [loading, setLoading] = useState(false);
  const { login } = useAuth();
  const router = useRouter();

  useEffect(() => {
    api
      .get<{ databases: DbEntry[]; default: string }>('/auth/databases')
      .then(({ data }) => {
        setDatabases(data.databases || []);
        setCompanyDb(data.default || data.databases?.[0]?.code || '');
      })
      .catch(() => {});
  }, []);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError('');
    setLoading(true);
    try {
      await login(username, password, companyDb);
      router.push('/dashboard');
    } catch (err) {
      const ae = err as { response?: { data?: { detail?: string; message?: string } } };
      setError(ae.response?.data?.detail || ae.response?.data?.message || 'Sai tên đăng nhập hoặc mật khẩu');
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="min-h-screen flex items-center justify-center bg-[#f0f0f0] p-4">
      <div className="bg-white rounded-lg shadow-lg w-full max-w-sm overflow-hidden">
        <div className="h-1 bg-brand-yellow"></div>
        <div className="p-8">
          <div className="flex justify-center mb-4">
            <div className="w-12 h-12 bg-navy-900 rounded flex items-center justify-center text-brand-yellow font-bold">
              SO
            </div>
          </div>
          <div className="border border-navy-900 rounded px-4 py-2 text-center mb-1">
            <span className="font-semibold text-navy-900 text-sm">Sale Order</span>
          </div>
          <p className="text-center text-xs text-slate-500 mb-6">
            Hệ thống quản lý đơn hàng bán - SAP B1 HANA
          </p>
          <form onSubmit={handleSubmit} className="space-y-4">
            <div>
              <label className="text-xs font-medium text-slate-700 mb-1 block">Tên đăng nhập</label>
              <input
                type="text"
                value={username}
                onChange={(e) => setUsername(e.target.value)}
                className="w-full px-3 py-2 text-sm border border-slate-300 rounded focus:outline-none focus:border-brand-blue"
                required
              />
            </div>
            <div>
              <label className="text-xs font-medium text-slate-700 mb-1 block">Mật khẩu</label>
              <input
                type="password"
                value={password}
                onChange={(e) => setPassword(e.target.value)}
                className="w-full px-3 py-2 text-sm border border-slate-300 rounded focus:outline-none focus:border-brand-blue"
                required
              />
            </div>
            <div>
              <label className="text-xs font-medium text-slate-700 mb-1 block">Cơ sở dữ liệu</label>
              <select
                value={companyDb}
                onChange={(e) => setCompanyDb(e.target.value)}
                className="w-full px-3 py-2 text-sm border border-slate-300 rounded focus:outline-none focus:border-brand-blue bg-white"
              >
                {databases.map((db) => (
                  <option key={db.code} value={db.code}>
                    {db.code}
                  </option>
                ))}
              </select>
              <p className="text-xs text-slate-400 mt-1">
                Chọn CompanyDB SAP B1 để làm việc trong phiên này
              </p>
            </div>
            {error && (
              <div className="bg-red-50 border border-red-200 text-red-700 px-3 py-2 rounded text-sm">
                {error}
              </div>
            )}
            <button
              type="submit"
              disabled={loading}
              className="w-full bg-navy-800 hover:bg-navy-900 text-white font-medium py-2.5 rounded text-sm transition-colors disabled:opacity-50"
            >
              {loading ? 'Đang đăng nhập...' : 'Đăng nhập'}
            </button>
          </form>
        </div>
      </div>
    </div>
  );
}
