import { useState, useEffect } from 'react';
import Layout from '../components/Layout';
import api from '../api/client';

export default function Users() {
  const [users, setUsers] = useState([]);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    api.get('/auth/users')
      .then(({ data }) => setUsers(data.items || data || []))
      .catch(() => {})
      .finally(() => setLoading(false));
  }, []);

  const roleBadge = (role) => {
    const colors = {
      admin: 'bg-red-100 text-red-800',
      manager: 'bg-brand-yellow/30 text-yellow-800',
      sales: 'bg-blue-100 text-blue-800',
      viewer: 'bg-slate-100 text-slate-600',
    };
    return <span className={`badge ${colors[role] || 'bg-slate-100'}`}>{role}</span>;
  };

  return (
    <Layout title="Người dùng">
      <div className="card !p-0 overflow-hidden">
        {loading ? (
          <div className="text-center py-12 text-slate-500">Đang tải…</div>
        ) : (
          <div className="overflow-x-auto">
            <table className="w-full text-sm min-w-[500px]">
              <thead className="bg-slate-50 border-b">
                <tr>
                  <th className="text-left px-4 py-3">Tên đăng nhập</th>
                  <th className="text-left px-4 py-3">Vai trò</th>
                  <th className="text-left px-4 py-3">Trạng thái</th>
                </tr>
              </thead>
              <tbody>
                {users.map((u) => (
                  <tr key={u.username} className="border-b hover:bg-slate-50">
                    <td className="px-4 py-3 font-medium">{u.username}</td>
                    <td className="px-4 py-3">{roleBadge(u.role)}</td>
                    <td className="px-4 py-3">
                      <span className={`badge ${u.is_active ? 'badge-open' : 'badge-cancelled'}`}>
                        {u.is_active ? 'Hoạt động' : 'Khóa'}
                      </span>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </div>
    </Layout>
  );
}
