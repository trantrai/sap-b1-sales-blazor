import { useState, useEffect } from 'react';
import { Link } from 'react-router-dom';
import Layout from '../components/Layout';
import api from '../api/client';

export default function Dashboard() {
  const [stats, setStats] = useState(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    api.get('/orders/stats')
      .then(({ data }) => setStats(data))
      .catch(() => {})
      .finally(() => setLoading(false));
  }, []);

  if (loading) {
    return (
      <Layout title="Dashboard">
        <div className="text-center py-12 text-slate-500">Đang tải…</div>
      </Layout>
    );
  }

  const cards = [
    { label: 'Tổng đơn hàng', value: stats?.total_orders ?? 0, icon: '📋', color: 'bg-blue-500' },
    { label: 'Đơn mở', value: stats?.open_orders ?? 0, icon: '📂', color: 'bg-green-500' },
    { label: 'Đơn đóng', value: stats?.closed_orders ?? 0, icon: '📁', color: 'bg-slate-500' },
    { label: 'Tổng giá trị', value: stats?.total_amount?.toLocaleString('vi-VN') ?? 0, icon: '💰', color: 'bg-brand-yellow' },
  ];

  return (
    <Layout title="Dashboard">
      <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4 mb-6">
        {cards.map((card) => (
          <div key={card.label} className="card">
            <div className="flex items-center gap-4">
              <div className={`${card.color} w-12 h-12 rounded-xl flex items-center justify-center text-2xl`}>
                {card.icon}
              </div>
              <div>
                <div className="text-2xl font-bold text-navy-900">{card.value}</div>
                <div className="text-sm text-slate-500">{card.label}</div>
              </div>
            </div>
          </div>
        ))}
      </div>

      <div className="card">
        <h3 className="font-bold text-navy-900 mb-4">Thao tác nhanh</h3>
        <div className="flex flex-wrap gap-3">
          <Link to="/orders/new" className="btn-primary">➕ Tạo đơn mới</Link>
          <Link to="/" className="btn-secondary">📋 Xem đơn hàng</Link>
          <Link to="/sync" className="btn-secondary">🔄 Giám sát tích hợp</Link>
        </div>
      </div>
    </Layout>
  );
}
