// app/monitor/page.tsx — Giám sát tích hợp Service Layer.
'use client';

import { useState, useEffect } from 'react';
import Layout from '@/components/Layout';
import api from '@/lib/api-client';

interface LogItem {
  action: string;
  status: string;
  username?: string;
  doc_num?: number;
  external_id?: string;
  error?: string;
  duration_ms?: number;
  timestamp?: string;
  created_at?: string;
}

export default function Monitor() {
  const [activeTab, setActiveTab] = useState('service');
  const [logs, setLogs] = useState<LogItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [actionFilter, setActionFilter] = useState('');
  const [resultFilter, setResultFilter] = useState('');
  const [search, setSearch] = useState('');

  const load = () => {
    setLoading(true);
    api
      .get<{ items: LogItem[] }>('/sync/audit-log', { params: { page: 1, page_size: 15 } })
      .then(({ data }) => setLogs(data.items || []))
      .catch(() => {})
      .finally(() => setLoading(false));
  };

  useEffect(() => {
    load();
  }, []);

  const handleRetryAll = async () => {
    if (!confirm('Thử lại tất cả thao tác thất bại?')) return;
    try {
      await api.post('/sync/retry-failed');
      alert('Đã thử lại');
      load();
    } catch {
      alert('Thất bại');
    }
  };

  const filtered = logs.filter((log) => {
    if (actionFilter && log.action !== actionFilter) return false;
    if (resultFilter && resultFilter === 'success') return log.status === 'success';
    if (resultFilter && resultFilter === 'failed') return log.status !== 'success';
    if (search) {
      const q = search.toLowerCase();
      const hay =
        `${log.doc_num || ''} ${log.external_id || ''} ${log.username || ''} ${log.error || ''}`.toLowerCase();
      if (!hay.includes(q)) return false;
    }
    return true;
  });

  return (
    <Layout title="Giám sát tích hợp">
      <div className="bg-white rounded-lg border border-slate-200 p-5 mb-4">
        <h3 className="font-semibold text-navy-900 text-sm mb-2">Giám sát tích hợp Service Layer</h3>
        <p className="text-xs text-slate-500 leading-relaxed">
          Mọi thao tác gọi lên SAP B1 (gọi tạo / hủy / đóng đơn) đều được ghi lại kèm request,
          response, độ trễ và thời gian xử lý. Thao tác thất bại có thể thử lại — hệ thống tự kiểm tra
          idempotency để tránh tạo trùng đơn.
        </p>
      </div>

      <div className="bg-white rounded-lg border border-slate-200 p-5">
        <div className="flex gap-4 border-b mb-4">
          <button
            onClick={() => setActiveTab('service')}
            className={`pb-2.5 text-sm font-medium flex items-center gap-1.5 ${
              activeTab === 'service' ? 'text-navy-900 border-b-2 border-navy-900' : 'text-slate-500'
            }`}
          >
            🔍 Service Layer
          </button>
          <button
            onClick={() => setActiveTab('user')}
            className={`pb-2.5 text-sm font-medium ${
              activeTab === 'user' ? 'text-navy-900 border-b-2 border-navy-900' : 'text-slate-500'
            }`}
          >
            Thao tác người dùng
          </button>
        </div>

        <div className="flex flex-wrap items-center gap-2 mb-4">
          <select
            value={actionFilter}
            onChange={(e) => setActionFilter(e.target.value)}
            className="px-3 py-1.5 text-sm border border-slate-300 rounded bg-white"
          >
            <option value="">Tất cả thao tác</option>
            <option value="order.create">order.create</option>
            <option value="order.cancel">order.cancel</option>
            <option value="order.close">order.close</option>
          </select>
          <select
            value={resultFilter}
            onChange={(e) => setResultFilter(e.target.value)}
            className="px-3 py-1.5 text-sm border border-slate-300 rounded bg-white"
          >
            <option value="">Tất cả kết quả</option>
            <option value="success">Thành công</option>
            <option value="failed">Thất bại</option>
          </select>
          <input
            type="text"
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            placeholder="Mã đơn / external id / người dùng / lỗi"
            className="px-3 py-1.5 text-sm border border-slate-300 rounded w-64"
          />
          <button onClick={load} className="px-3 py-1.5 text-sm text-slate-600 hover:text-navy-900">
            Làm mới
          </button>
          <span className="ml-auto text-xs text-slate-400">15 bản ghi</span>
          <button
            onClick={handleRetryAll}
            className="px-4 py-1.5 bg-navy-800 text-white text-sm rounded hover:bg-navy-900"
          >
            Thử lại tất cả thất bại
          </button>
        </div>

        {loading ? (
          <div className="text-center py-8 text-sm text-slate-500">Đang tải...</div>
        ) : (
          <div className="overflow-x-auto">
            <table className="w-full text-sm min-w-[900px]">
              <thead>
                <tr className="text-xs text-slate-500 border-b">
                  <th className="text-left py-2 font-medium w-8">#</th>
                  <th className="text-left py-2 font-medium">Thời gian</th>
                  <th className="text-left py-2 font-medium">Thao tác</th>
                  <th className="text-left py-2 font-medium">Đơn / Tham chiếu</th>
                  <th className="text-left py-2 font-medium">Người dùng</th>
                  <th className="text-left py-2 font-medium">Kết quả</th>
                  <th className="text-left py-2 font-medium">Thời gian XL</th>
                  <th className="text-left py-2 font-medium">Thử lại</th>
                  <th className="text-left py-2 font-medium">Lỗi</th>
                </tr>
              </thead>
              <tbody>
                {filtered.map((log, i) => (
                  <tr key={i} className="border-b last:border-0 hover:bg-slate-50">
                    <td className="py-2.5 text-slate-400">{filtered.length - i}</td>
                    <td className="py-2.5 text-xs">{log.timestamp || log.created_at}</td>
                    <td className="py-2.5 font-mono text-xs">{log.action}</td>
                    <td className="py-2.5">{log.doc_num || log.external_id || '—'}</td>
                    <td className="py-2.5">{log.username}</td>
                    <td className="py-2.5">
                      <span
                        className={`text-xs px-2 py-0.5 rounded-full font-medium ${
                          log.status === 'success'
                            ? 'bg-green-100 text-green-700'
                            : 'bg-red-100 text-red-700'
                        }`}
                      >
                        {log.status === 'success' ? 'Thành công' : 'Thất bại'}
                      </span>
                    </td>
                    <td className="py-2.5">{log.duration_ms ? `${log.duration_ms} ms` : '—'}</td>
                    <td className="py-2.5 text-slate-400">—</td>
                    <td
                      className="py-2.5 text-xs text-red-600 max-w-[200px] truncate"
                      title={log.error}
                    >
                      {log.error || '—'}
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
