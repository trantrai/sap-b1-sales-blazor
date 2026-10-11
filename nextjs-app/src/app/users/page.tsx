// app/users/page.tsx — Người dùng & phân quyền.
'use client';

import { useState, useEffect } from 'react';
import Layout from '@/components/Layout';
import api from '@/lib/api-client';

const rbacMatrix: { permission: string; roles: boolean[] }[] = [
  { permission: 'Xem danh sách & chi tiết đơn hàng', roles: [true, true, true, true] },
  { permission: 'Tra cứu KH / hàng hoá / thuế suất', roles: [true, true, true, true] },
  { permission: 'Xem giám sát tích hợp', roles: [true, true, true, true] },
  { permission: 'Tạo đơn hàng', roles: [false, true, true, true] },
  { permission: 'Sửa đơn hàng (ghi chú, ngày giao...)', roles: [false, true, true, true] },
  { permission: 'Huỷ / đóng đơn hàng', roles: [false, false, true, true] },
  { permission: 'Đồng bộ master data vào DB local', roles: [false, false, true, true] },
  { permission: 'Thử lại thao tác tích hợp lỗi', roles: [false, false, false, true] },
  { permission: 'Quản lý người dùng & phân quyền', roles: [false, false, false, true] },
  { permission: 'Cài đặt kết nối Service Layer', roles: [false, false, false, true] },
];

const roleHeaders = ['Người xem', 'Nhân viên kinh doanh', 'Quản lý', 'Quản trị viên'];

interface UserRow {
  username: string;
  full_name?: string;
  role: string;
  is_active?: boolean;
}

export default function Users() {
  const [users, setUsers] = useState<UserRow[]>([]);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    api
      .get<UserRow[] | { items: UserRow[] }>('/auth/users')
      .then(({ data }) => setUsers(Array.isArray(data) ? data : data.items || []))
      .catch(() => {})
      .finally(() => setLoading(false));
  }, []);

  return (
    <Layout title="Người dùng & phân quyền">
      <div className="bg-white rounded-lg border border-slate-200 p-5 mb-4">
        <div className="flex items-center justify-between mb-4">
          <h3 className="font-semibold text-navy-900 text-sm">Người dùng ({users.length})</h3>
          <button className="px-4 py-2 bg-navy-800 text-white text-sm font-medium rounded hover:bg-navy-900 flex items-center gap-1.5">
            ➕ Thêm người dùng
          </button>
        </div>
        {loading ? (
          <div className="text-center py-8 text-sm text-slate-500">Đang tải...</div>
        ) : (
          <div className="overflow-x-auto">
            <table className="w-full text-sm min-w-[700px]">
              <thead>
                <tr className="text-xs text-slate-500 border-b">
                  <th className="text-left py-2 font-medium">Tên đăng nhập</th>
                  <th className="text-left py-2 font-medium">Họ tên</th>
                  <th className="text-left py-2 font-medium">Vai trò</th>
                  <th className="text-left py-2 font-medium">MS NVKD</th>
                  <th className="text-left py-2 font-medium">Trạng thái</th>
                  <th className="text-right py-2 font-medium"></th>
                </tr>
              </thead>
              <tbody>
                {users.map((u) => (
                  <tr key={u.username} className="border-b last:border-0">
                    <td className="py-3 font-medium">{u.username}</td>
                    <td className="py-3 text-slate-600">
                      {u.full_name || (u.username === 'admin' ? 'Administrator' : u.username)}
                    </td>
                    <td className="py-3">
                      <span className="bg-slate-100 text-slate-600 text-xs px-2 py-0.5 rounded-full font-mono">
                        {u.role}
                      </span>
                    </td>
                    <td className="py-3 text-slate-400">—</td>
                    <td className="py-3">
                      <span
                        className={`text-xs px-2.5 py-0.5 rounded-full font-medium ${
                          u.is_active !== false
                            ? 'bg-green-100 text-green-700'
                            : 'bg-red-100 text-red-700'
                        }`}
                      >
                        {u.is_active !== false ? 'Hoạt động' : 'Đã khoá'}
                      </span>
                    </td>
                    <td className="py-3 text-right">
                      <button className="text-sm text-slate-500 hover:text-navy-900 mr-3">Sửa</button>
                      <button className="text-sm text-slate-500 hover:text-red-600">Khoá</button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </div>

      <div className="bg-white rounded-lg border border-slate-200 p-5">
        <h3 className="font-semibold text-navy-900 text-sm mb-1">Ma trận phân quyền RBAC</h3>
        <p className="text-xs text-slate-500 mb-4">
          Quyền tối thiểu: vai trò cao hơn có mọi quyền của vai trò thấp hơn
        </p>
        <div className="overflow-x-auto">
          <table className="w-full text-sm min-w-[700px]">
            <thead>
              <tr className="text-xs text-slate-500 border-b">
                <th className="text-left py-2 font-medium">Quyền</th>
                {roleHeaders.map((r) => (
                  <th key={r} className="text-center py-2 font-medium">
                    {r}
                  </th>
                ))}
              </tr>
            </thead>
            <tbody>
              {rbacMatrix.map((row, i) => (
                <tr key={i} className="border-b last:border-0">
                  <td className="py-2.5 text-navy-900">{row.permission}</td>
                  {row.roles.map((has, j) => (
                    <td key={j} className="py-2.5 text-center">
                      {has ? (
                        <span className="inline-flex w-5 h-5 bg-green-100 text-green-600 rounded-full items-center justify-center text-xs">
                          ✓
                        </span>
                      ) : (
                        <span className="text-slate-300">—</span>
                      )}
                    </td>
                  ))}
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </div>
    </Layout>
  );
}
