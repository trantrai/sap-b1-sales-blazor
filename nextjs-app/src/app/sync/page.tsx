// app/sync/page.tsx — Đồng bộ master data.
'use client';

import { useState, useEffect } from 'react';
import Layout from '@/components/Layout';
import api from '@/lib/api-client';

interface EntityStatus {
  count?: number;
  last_sync?: string;
  new_records?: number;
}

export default function Sync() {
  const [status, setStatus] = useState<Record<string, EntityStatus> | null>(null);
  const [loading, setLoading] = useState(true);
  const [syncing, setSyncing] = useState(false);

  const load = () => {
    api
      .get<Record<string, EntityStatus>>('/sync/status')
      .then(({ data }) => setStatus(data))
      .catch(() => {})
      .finally(() => setLoading(false));
  };

  useEffect(() => {
    load();
  }, []);

  const handleSync = async () => {
    setSyncing(true);
    try {
      await api.post('/sync/all');
      load();
    } catch (err) {
      const ae = err as { response?: { data?: { detail?: string; message?: string } } };
      alert(ae.response?.data?.detail || ae.response?.data?.message || 'Đồng bộ thất bại');
    } finally {
      setSyncing(false);
    }
  };

  const entities = [
    { key: 'customers', label: 'Khách hàng' },
    { key: 'items', label: 'Hàng hoá' },
    { key: 'tax_codes', label: 'Thuế suất' },
  ];

  return (
    <Layout title="Đồng bộ dữ liệu">
      <div className="bg-white rounded-lg border border-slate-200 p-5 mb-4">
        <h3 className="font-semibold text-navy-900 text-sm mb-2">Đồng bộ master data từ SAP B1</h3>
        <p className="text-xs text-slate-500 leading-relaxed">
          SAP B1 là nguồn dữ liệu gốc. Hệ thống lưu bản sao Khách hàng, Mặt hàng và Thuế suất vào cơ
          sở dữ liệu local để tra cứu khi tạo đơn hàng (không phải gọi B1 mỗi lần gõ). Hãy đồng bộ lại
          khi master data trong B1 có thay đổi.
        </p>
      </div>

      <div className="bg-white rounded-lg border border-slate-200 p-5">
        <div className="flex items-center justify-between mb-4">
          <h4 className="font-semibold text-navy-900 text-sm">Trạng thái đồng bộ</h4>
          <button
            onClick={handleSync}
            disabled={syncing}
            className="px-4 py-2 bg-navy-800 text-white text-sm font-medium rounded hover:bg-navy-900 disabled:opacity-50"
          >
            {syncing ? 'Đang đồng bộ...' : '🔄 Đồng bộ ngay'}
          </button>
        </div>
        {loading ? (
          <div className="text-center py-8 text-sm text-slate-500">Đang tải...</div>
        ) : (
          <div className="overflow-x-auto">
            <table className="w-full text-sm min-w-[600px]">
              <thead>
                <tr className="text-xs text-slate-500 border-b">
                  <th className="text-left py-2 font-medium">Dữ liệu</th>
                  <th className="text-left py-2 font-medium">Số bản ghi (local)</th>
                  <th className="text-left py-2 font-medium">Đồng bộ lần cuối</th>
                  <th className="text-left py-2 font-medium">Trạng thái</th>
                </tr>
              </thead>
              <tbody>
                {entities.map((e) => {
                  const s = status?.[e.key] || {};
                  return (
                    <tr key={e.key} className="border-b last:border-0">
                      <td className="py-3 font-medium text-navy-900">{e.label}</td>
                      <td className="py-3">{s.count ?? '—'}</td>
                      <td className="py-3">
                        <div>{s.last_sync || '—'}</div>
                        {s.new_records && s.new_records > 0 && (
                          <div className="text-xs text-slate-400">{s.new_records} bản ghi mới</div>
                        )}
                      </td>
                      <td className="py-3">
                        <span className="bg-green-100 text-green-700 text-xs px-2.5 py-0.5 rounded-full font-medium">
                          Thành công
                        </span>
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
        )}
        <p className="text-xs text-slate-400 mt-4">
          Chỉ tài khoản quản trị (manager/admin) mới được chạy đồng bộ.
        </p>
      </div>
    </Layout>
  );
}
