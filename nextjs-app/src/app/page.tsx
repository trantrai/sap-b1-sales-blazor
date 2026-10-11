// app/page.tsx — Danh sách đơn hàng (trang chủ).
'use client';

import { useState, useEffect, useCallback, Suspense } from 'react';
import Link from 'next/link';
import { useSearchParams, useRouter } from 'next/navigation';
import Layout from '@/components/Layout';
import api from '@/lib/api-client';
import type { OrderOut } from '@/lib/b1-client';


const statusBadge = (status: string) => {
  const map: Record<string, string> = {
    open: 'bg-blue-100 text-blue-700',
    closed: 'bg-slate-100 text-slate-600',
    cancelled: 'bg-red-100 text-red-700',
  };
  const labels: Record<string, string> = { open: 'Đang mở', closed: 'Đã đóng', cancelled: 'Đã hủy' };
  return (
    <span
      className={`inline-block px-2.5 py-0.5 rounded-full text-xs font-medium ${map[status] || 'bg-slate-100'}`}
    >
      {labels[status] || status}
    </span>
  );
};

function OrdersInner() {
  const searchParams = useSearchParams();
  const router = useRouter();
  const [orders, setOrders] = useState<OrderOut[]>([]);
  const [total, setTotal] = useState(0);
  const [loading, setLoading] = useState(true);

  const status = searchParams.get('status') || '';
  const cardCode = searchParams.get('card_code') || '';
  const docNum = searchParams.get('doc_num') || '';
  const page = parseInt(searchParams.get('page') || '1', 10);
  const pageSize = 20;

  const [localStatus, setLocalStatus] = useState(status);
  const [localCardCode, setLocalCardCode] = useState(cardCode);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const params: Record<string, unknown> = { page, page_size: pageSize };
      if (status) params.status = status;
      if (cardCode) params.card_code = cardCode;
      if (docNum) params.doc_num = docNum;
      const { data } = await api.get<{ items: OrderOut[]; total: number }>('/orders', { params });
      setOrders(data.items || []);
      setTotal(data.total || 0);
    } catch {
      setOrders([]);
    } finally {
      setLoading(false);
    }
  }, [status, cardCode, docNum, page]);

  useEffect(() => {
    load();
  }, [load]);
  useEffect(() => {
    setLocalStatus(status);
    setLocalCardCode(cardCode);
  }, [status, cardCode]);

  const applyFilter = () => {
    const next = new URLSearchParams(searchParams.toString());
    if (localStatus) next.set('status', localStatus);
    else next.delete('status');
    if (localCardCode) next.set('card_code', localCardCode);
    else next.delete('card_code');
    next.set('page', '1');
    router.push(`/?${next.toString()}`);
  };

  const gotoPage = (p: number) => {
    const next = new URLSearchParams(searchParams.toString());
    next.set('page', String(p));
    router.push(`/?${next.toString()}`);
  };

  const totalPages = Math.ceil(total / pageSize);

  const handleExport = async () => {
    try {
      const params: Record<string, unknown> = {};
      if (status) params.status = status;
      if (cardCode) params.card_code = cardCode;
      if (docNum) params.doc_num = docNum;
      const res = await api.get<Blob>('/orders/export', { params, responseType: 'blob' });
      const url = URL.createObjectURL(res.data);
      const a = document.createElement('a');
      a.href = url;
      a.download = 'don-hang.xlsx';
      a.click();
    } catch {
      alert('Xuất Excel thất bại');
    }
  };

  return (
    <Layout title="Danh sách đơn">
      <div className="flex flex-wrap items-center gap-2 mb-3">
        <select
          value={localStatus}
          onChange={(e) => setLocalStatus(e.target.value)}
          className="px-3 py-2 text-sm border border-slate-300 rounded bg-white min-w-[140px]"
        >
          <option value="">Trạng thái</option>
          <option value="open">Đang mở</option>
          <option value="closed">Đã đóng</option>
          <option value="cancelled">Đã hủy</option>
        </select>
        <input
          type="text"
          value={localCardCode}
          onChange={(e) => setLocalCardCode(e.target.value)}
          placeholder="Mã KH (VD: C20000)"
          className="px-3 py-2 text-sm border border-slate-300 rounded w-44"
        />
        <button
          onClick={applyFilter}
          className="px-5 py-2 bg-navy-800 hover:bg-navy-900 text-white text-sm font-medium rounded transition-colors"
        >
          Lọc
        </button>
        <button
          onClick={handleExport}
          className="px-4 py-2 border border-slate-300 text-sm rounded hover:bg-slate-50 flex items-center gap-1.5"
        >
          <span>⬇</span> Xuất Excel
        </button>
        <span className="ml-auto text-xs text-slate-500">{total} đơn hàng</span>
      </div>

      <div className="bg-white rounded-lg border border-slate-200 overflow-hidden">
        {loading ? (
          <div className="text-center py-12 text-slate-500 text-sm">Đang tải...</div>
        ) : orders.length === 0 ? (
          <div className="text-center py-12 text-slate-500 text-sm">Không có đơn hàng</div>
        ) : (
          <div className="overflow-x-auto">
            <table className="w-full text-sm min-w-[760px]">
              <thead>
                <tr className="text-xs text-slate-500 border-b bg-slate-50/50">
                  <th className="text-left px-4 py-2.5 font-medium">Số đơn</th>
                  <th className="text-left px-4 py-2.5 font-medium">Ngày</th>
                  <th className="text-left px-4 py-2.5 font-medium">Khách hàng</th>
                  <th className="text-right px-4 py-2.5 font-medium">Tổng tiền</th>
                  <th className="text-left px-4 py-2.5 font-medium">Trạng thái</th>
                </tr>
              </thead>
              <tbody>
                {orders.map((o) => (
                  <tr key={o.doc_entry} className="border-b last:border-0 hover:bg-slate-50">
                    <td className="px-4 py-3">
                      <Link
                        href={`/orders/${o.doc_entry}`}
                        className="text-brand-blue font-semibold hover:underline"
                      >
                        #{o.doc_num}
                      </Link>
                      <div className="text-xs text-slate-400">ID nội bộ: {o.doc_entry}</div>
                    </td>
                    <td className="px-4 py-3 text-slate-600">{o.doc_date}</td>
                    <td className="px-4 py-3">
                      <div className="font-medium text-navy-900">{o.card_name}</div>
                      <div className="text-xs text-slate-400">{o.card_code}</div>
                    </td>
                    <td className="px-4 py-3 text-right font-semibold">
                      {o.doc_total.toLocaleString('vi-VN')}
                    </td>
                    <td className="px-4 py-3">{statusBadge(o.status)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </div>

      {totalPages > 1 && (
        <div className="flex items-center justify-end gap-2 mt-3 text-sm">
          <span className="text-slate-500">
            Trang {page} / {totalPages}
          </span>
          <button
            disabled={page <= 1}
            onClick={() => gotoPage(page - 1)}
            className="px-3 py-1.5 border rounded text-sm disabled:opacity-40 hover:bg-slate-50"
          >
            ←
          </button>
          <button
            disabled={page >= totalPages}
            onClick={() => gotoPage(page + 1)}
            className="px-3 py-1.5 border rounded text-sm disabled:opacity-40 hover:bg-slate-50"
          >
            →
          </button>
        </div>
      )}
    </Layout>
  );
}

export default function Orders() {
  return (
    <Suspense fallback={<Layout title="Danh sách đơn"><div className="text-center py-12 text-sm">Đang tải...</div></Layout>}>
      <OrdersInner />
    </Suspense>
  );
}
