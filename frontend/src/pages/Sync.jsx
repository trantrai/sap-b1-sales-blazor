import { useState, useEffect } from 'react';
import Layout from '../components/Layout';
import api from '../api/client';

export default function Sync() {
  const [status, setStatus] = useState(null);
  const [loading, setLoading] = useState(true);

  const load = () => {
    api.get('/sync/status')
      .then(({ data }) => setStatus(data))
      .catch(() => {})
      .finally(() => setLoading(false));
  };

  useEffect(() => { load(); }, []);

  const handleSync = async (entity) => {
    try {
      await api.post(`/sync/${entity}`);
      alert('Đã bắt đầu đồng bộ');
      load();
    } catch (err) {
      alert(err.response?.data?.message || 'Thất bại');
    }
  };

  return (
    <Layout title="Giám sát tích hợp">
      <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
        {['customers', 'items', 'orders'].map((entity) => (
          <div key={entity} className="card">
            <h3 className="font-bold text-navy-900 mb-2 capitalize">{entity}</h3>
            <p className="text-sm text-slate-500 mb-4">
              Lần cuối: {status?.[entity]?.last_sync || 'Chưa đồng bộ'}
            </p>
            <button onClick={() => handleSync(entity)} className="btn-primary">
              🔄 Đồng bộ ngay
            </button>
          </div>
        ))}
      </div>
      {loading && <div className="text-center py-8 text-slate-500">Đang tải…</div>}
    </Layout>
  );
}
