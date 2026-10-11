// app/audit/page.tsx — Audit người dùng.
'use client';

import { useState, useEffect } from 'react';
import Layout from '@/components/Layout';
import api from '@/lib/api-client';

interface AuditLog {
  username?: string;
  action?: string;
  entity_type?: string;
  entity_id?: string;
  ip?: string;
  timestamp?: string;
  created_at?: string;
}

export default function Audit() {
  const [logs, setLogs] = useState<AuditLog[]>([]);
  const [loading, setLoading] = useState(true);
  const [page, setPage] = useState(1);
  const pageSize = 20;

  useEffect(() => {
    setLoading(true);
    api
      .get<{ items: AuditLog[] }>('/audit', { params: { page, page_size: pageSize } })
      .then(({ data }) => setLogs(data.items || []))
      .catch(() => {})
      .finally(() => setLoading(false));
  }, [page]);

  return (
    <Layout title="Audit người dùng">
      <div className="bg-white rounded-lg border border-slate-200 overflow-hidden">
        {loading ? (
          <div className="text-center py-12 text-sm text-slate-500">Đang tải...</div>
        ) : (
          <div className="overflow-x-auto">
            <table className="w-full text-sm min-w-[800px]">
              <thead>
                <tr className="text-xs text-slate-500 border-b bg-slate-50/50">
                  <th className="text-left px-4 py-2.5 font-medium">Thời gian</th>
                  <th className="text-left px-4 py-2.5 font-medium">Người dùng</th>
                  <th className="text-left px-4 py-2.5 font-medium">Hành động</th>
                  <th className="text-left px-4 py-2.5 font-medium">Đối tượng</th>
                  <th className="text-left px-4 py-2.5 font-medium">Mã</th>
                  <th className="text-left px-4 py-2.5 font-medium">IP</th>
                </tr>
              </thead>
              <tbody>
                {logs.map((log, i) => (
                  <tr key={i} className="border-b last:border-0 hover:bg-slate-50">
                    <td className="px-4 py-2.5 text-xs text-slate-500 whitespace-nowrap">
                      {log.timestamp || log.created_at}
                    </td>
                    <td className="px-4 py-2.5 font-medium">{log.username}</td>
                    <td className="px-4 py-2.5">{log.action}</td>
                    <td className="px-4 py-2.5 text-slate-600">{log.entity_type}</td>
                    <td className="px-4 py-2.5 font-mono text-xs">{log.entity_id || '—'}</td>
                    <td className="px-4 py-2.5 text-xs text-slate-400">{log.ip || '::1'}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </div>
      <div className="flex items-center justify-end gap-2 mt-3">
        <button
          disabled={page <= 1}
          onClick={() => setPage(page - 1)}
          className="px-3 py-1.5 text-sm border rounded disabled:opacity-40 hover:bg-white bg-white"
        >
          ← Trước
        </button>
        <span className="text-sm text-slate-500">Trang {page}</span>
        <button
          onClick={() => setPage(page + 1)}
          className="px-3 py-1.5 text-sm border rounded hover:bg-white bg-white"
        >
          Sau →
        </button>
      </div>
    </Layout>
  );
}
