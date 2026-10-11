// app/dashboard/page.tsx — Dashboard kinh doanh.
'use client';

import { useState, useEffect } from 'react';
import Layout from '@/components/Layout';
import api from '@/lib/api-client';
import type { DashboardStats } from '@/lib/b1-client';

export default function Dashboard() {
  const [stats, setStats] = useState<DashboardStats | null>(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    api
      .get<DashboardStats>('/orders/stats')
      .then(({ data }) => setStats(data))
      .catch(() => {})
      .finally(() => setLoading(false));
  }, []);

  if (loading) {
    return (
      <Layout title="Dashboard">
        <div className="text-center py-12 text-slate-500">Đang tải...</div>
      </Layout>
    );
  }

  const statCards = [
    { label: 'Tổng đơn hàng', value: stats?.total_orders ?? 0, color: 'text-navy-900' },
    {
      label: 'Doanh số',
      value: (stats?.total_revenue ?? 0).toLocaleString('vi-VN'),
      color: 'text-navy-900',
    },
    { label: 'Đang mở', value: stats?.open_orders ?? 0, color: 'text-brand-orange' },
    { label: 'Đã đóng', value: stats?.closed_orders ?? 0, color: 'text-green-600' },
    { label: 'Đã hủy', value: stats?.cancelled_orders ?? 0, color: 'text-red-600' },
  ];

  const revenueByDay = stats?.revenue_by_day || [];
  const maxValue = Math.max(...revenueByDay.map((d) => d.revenue), 1);
  const chartLabels = revenueByDay.filter((_, i) => i % 5 === 0).map((d) => d.date);

  const topCustomers = stats?.top_customers || [];
  const topItems = stats?.top_items || [];

  return (
    <Layout title="Dashboard">
      <div className="bg-white rounded-lg border border-slate-200 p-5 mb-4">
        <h3 className="font-semibold text-navy-900 mb-1">Dashboard kinh doanh</h3>
        <p className="text-xs text-slate-500 mb-4">
          Tổng quan đơn hàng và doanh số — dữ liệu từ SAP B1. {stats?.scope_note || ''}
        </p>
        <div className="grid grid-cols-2 sm:grid-cols-3 lg:grid-cols-5 gap-3">
          {statCards.map((card) => (
            <div key={card.label} className="border border-slate-200 rounded-lg p-4 text-center">
              <div className="text-xs text-slate-500 mb-1">{card.label}</div>
              <div className={`text-2xl font-bold ${card.color}`}>{card.value}</div>
            </div>
          ))}
        </div>
      </div>

      <div className="bg-white rounded-lg border border-slate-200 p-5 mb-4">
        <h4 className="font-semibold text-navy-900 text-sm mb-4">Doanh số 30 ngày gần nhất</h4>
        <div className="relative h-48">
          <div className="absolute inset-0 flex items-end justify-between gap-1 px-2">
            {revenueByDay.map((d, i) => (
              <div
                key={i}
                className="flex-1 bg-navy-900 rounded-t"
                style={{
                  height: `${(d.revenue / maxValue) * 100}%`,
                  minHeight: d.revenue > 0 ? '4px' : '0',
                }}
                title={`${d.date}: ${d.revenue.toLocaleString('vi-VN')}`}
              />
            ))}
          </div>
        </div>
        <div className="flex justify-between text-xs text-slate-400 mt-2 px-2">
          {chartLabels.map((label) => (
            <span key={label}>{label}</span>
          ))}
        </div>
      </div>

      <div className="grid grid-cols-1 lg:grid-cols-2 gap-4">
        <div className="bg-white rounded-lg border border-slate-200 p-5">
          <h4 className="font-semibold text-navy-900 text-sm mb-3">Top 5 khách hàng</h4>
          <table className="w-full text-sm">
            <thead>
              <tr className="text-xs text-slate-500 border-b">
                <th className="text-left py-2 font-medium">Khách hàng</th>
                <th className="text-right py-2 font-medium">Đơn</th>
                <th className="text-right py-2 font-medium">Doanh số</th>
              </tr>
            </thead>
            <tbody>
              {topCustomers.map((c, i) => (
                <tr key={i} className="border-b last:border-0">
                  <td className="py-2.5">
                    <div className="font-medium text-navy-900">{c.card_name}</div>
                    <div className="text-xs text-slate-400">{c.card_code}</div>
                  </td>
                  <td className="text-right py-2.5">{c.orders}</td>
                  <td className="text-right py-2.5">{c.revenue.toLocaleString('vi-VN')}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
        <div className="bg-white rounded-lg border border-slate-200 p-5">
          <h4 className="font-semibold text-navy-900 text-sm mb-3">Top 5 hàng hoá</h4>
          <table className="w-full text-sm">
            <thead>
              <tr className="text-xs text-slate-500 border-b">
                <th className="text-left py-2 font-medium">Hàng hoá</th>
                <th className="text-right py-2 font-medium">SL</th>
                <th className="text-right py-2 font-medium">Doanh số</th>
              </tr>
            </thead>
            <tbody>
              {topItems.map((item, i) => (
                <tr key={i} className="border-b last:border-0">
                  <td className="py-2.5">
                    <div className="font-medium text-navy-900">{item.item_name}</div>
                    <div className="text-xs text-slate-400">{item.item_code}</div>
                  </td>
                  <td className="text-right py-2.5">{item.quantity}</td>
                  <td className="text-right py-2.5">{item.revenue.toLocaleString('vi-VN')}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </div>
    </Layout>
  );
}
