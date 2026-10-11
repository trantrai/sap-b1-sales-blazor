import { useState, useEffect } from 'react';
import Layout from '../components/Layout';
import api from '../api/client';

export default function Audit() {
  const [logs, setLogs] = useState([]);
  const [loading, setLoading] = useState(true);
  const [page, setPage] = useState(1);

  useEffect(() => {
    api.get('/audit', { params: { page, page_size: 20 } })
      .then(({ data }) => setLogs(data.items || data || []))
      .catch(() => {})
      .finally(() => setLoading(false));
  }, [page]);

  return (
    <Layout title="Nhật ký Audit">
      <div className="card !p-0 overflow-hidden">
        {loading ? (
          <div className="text-center py-12 text-slate-500">Đang tải…</div>
        ) : (
          <div className="overflow-x-auto">
            <table className="w-full text-sm min-w-[700px]">
              <thead className="bg-slate-50 border-b">
                <tr>
                  <th className="text-left px-4 py-3">Thời gian</th>
                  <th className="text-left px-4 py-3">Người dùng</th>
                  <th className="text-left px-4 py-3">Hành động</th>
                  <th className="text-left px-4 py-3">Đối tượng</th>
                </tr>
              </thead>
              <tbody>
                {logs.map((log, i) => (
                  <tr key={i} className="border-b hover:bg-slate-50">
                    <td className="px-4 py-3 text-slate-500">{log.timestamp}</td>
                    <td className="px-4 py-3 font-medium">{log.username}</td>
                    <td className="px-4 py-3">{log.action}</td>
                    <td className="px-4 py-3">{log.entity_type}: {log.entity_id}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </div>
      <div className="flex justify-center gap-2 mt-4">
        <button
          disabled={page <= 1}
          onClick={() => setPage(page - 1)}
          className="btn-secondary !min-h-[36px] disabled:opacity-50"
        >
          ← Trước
        </button>
        <button onClick={() => setPage(page + 1)} className="btn-secondary !min-h-[36px]">
          Sau →
        </button>
      </div>
    </Layout>
  );
}
