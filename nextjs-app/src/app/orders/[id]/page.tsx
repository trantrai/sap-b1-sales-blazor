// app/orders/[id]/page.tsx — Chi tiết đơn hàng.
'use client';

import { useState, useEffect, ReactNode, Suspense } from 'react';
import { useParams, useRouter } from 'next/navigation';
import Link from 'next/link';
import Layout from '@/components/Layout';
import api from '@/lib/api-client';
import type { OrderOut, OrderLineOut } from '@/lib/b1-client';

function OrderDetailInner() {
  const params = useParams<{ id: string }>();
  const id = params.id;
  const router = useRouter();
  const [order, setOrder] = useState<OrderOut | null>(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    api
      .get<OrderOut>(`/orders/${id}`)
      .then(({ data }) => setOrder(data))
      .catch(() => {})
      .finally(() => setLoading(false));
  }, [id]);

  const handleAction = async (action: string, confirmMsg: string) => {
    if (!confirm(confirmMsg)) return;
    try {
      await api.post(`/orders/${id}/${action}`);
      const { data } = await api.get<OrderOut>(`/orders/${id}`);
      setOrder(data);
    } catch (err) {
      const ae = err as { response?: { data?: { detail?: string; message?: string } } };
      alert(ae.response?.data?.detail || ae.response?.data?.message || 'Thất bại');
    }
  };

  if (loading) {
    return (
      <Layout title="Chi tiết đơn">
        <div className="text-center py-12 text-sm text-slate-500">Đang tải...</div>
      </Layout>
    );
  }
  if (!order) {
    return (
      <Layout title="Chi tiết đơn">
        <div className="text-center py-12 text-sm text-slate-500">Không tìm thấy đơn hàng</div>
      </Layout>
    );
  }

  const infoItems: { label: string; value: ReactNode }[] = [
    {
      label: 'Khách hàng',
      value: (
        <>
          <div className="font-medium">{order.card_name}</div>
          <div className="text-xs text-slate-400">{order.card_code}</div>
        </>
      ),
    },
    { label: 'Số PO của KH', value: order.num_at_card || 'PO-2026-001' },
    { label: 'Ngày chứng từ', value: order.doc_date },
    { label: 'Ngày giao hàng', value: order.doc_date },
    { label: 'Ngày hạch toán', value: order.doc_date },
    { label: 'Tiền tệ / Tỷ giá', value: 'VND' },
    { label: 'Điều khoản thanh toán', value: 'Công nợ 30 ngày' },
    { label: 'Vận chuyển', value: 'Giao hàng tiêu chuẩn' },
    { label: 'Kho nhận hàng', value: '01' },
    { label: 'Hình thức vận chuyển', value: 'Trụ sở' },
    { label: 'Dự án', value: '—' },
    { label: 'Chiết khấu đơn hàng', value: `${order.discount_percent || 0}%` },
  ];

  const lines: OrderLineOut[] = order.lines || [];
  const subtotal = lines.reduce((s, l) => s + (l.quantity || 0) * (l.price || 0), 0);
  const docDisc = subtotal * ((order.discount_percent || 0) / 100);

  return (
    <Layout title="Chi tiết đơn">
      <div className="bg-white rounded-lg border border-slate-200 p-5 mb-4">
        <div className="flex flex-wrap items-center gap-2 mb-5">
          <Link href="/" className="text-sm text-slate-500 hover:text-navy-900">
            ← Danh sách
          </Link>
          <span className="font-semibold text-navy-900">Đơn hàng #{order.doc_num}</span>
          <span className="bg-blue-100 text-blue-700 text-xs px-2.5 py-0.5 rounded-full font-medium">
            {order.status === 'open' ? 'Đang mở' : order.status}
          </span>
          <div className="ml-auto flex flex-wrap gap-2">
            <button className="px-3 py-1.5 text-sm border border-slate-300 rounded hover:bg-slate-50 flex items-center gap-1.5">
              ✏️ Sửa ghi chú
            </button>
            <button
              onClick={() => router.push(`/orders/new?copy=${id}`)}
              className="px-3 py-1.5 text-sm border border-slate-300 rounded hover:bg-slate-50 flex items-center gap-1.5"
            >
              📋 Copy thành đơn mới
            </button>
            <button
              onClick={() => window.print()}
              className="px-3 py-1.5 text-sm border border-slate-300 rounded hover:bg-slate-50 flex items-center gap-1.5"
            >
              🖨️ In
            </button>
            {order.status === 'open' && (
              <>
                <button
                  onClick={() => handleAction('close', 'Đóng đơn hàng này?')}
                  className="px-3 py-1.5 text-sm border border-slate-300 rounded hover:bg-slate-50 flex items-center gap-1.5"
                >
                  ☑ Đóng đơn
                </button>
                <button
                  onClick={() => handleAction('cancel', 'Hủy đơn hàng này?')}
                  className="px-3 py-1.5 text-sm bg-brand-orange text-white rounded hover:bg-red-700 flex items-center gap-1.5"
                >
                  ✕ Hủy đơn
                </button>
              </>
            )}
          </div>
        </div>

        <div className="grid grid-cols-2 sm:grid-cols-3 lg:grid-cols-4 gap-x-6 gap-y-4 mb-5">
          {infoItems.map((item, i) => (
            <div key={i}>
              <div className="text-xs text-slate-400 mb-0.5">{item.label}</div>
              <div className="text-sm text-navy-900">{item.value}</div>
            </div>
          ))}
        </div>

        <div className="grid grid-cols-2 gap-4 mb-5">
          <div>
            <div className="text-xs text-slate-400 mb-0.5">Tổng tiền</div>
            <div className="text-sm font-bold text-navy-900">
              {(order.doc_total || 0).toLocaleString('vi-VN')} VND
            </div>
          </div>
          <div>
            <div className="text-xs text-slate-400 mb-0.5">Ghi chú</div>
            <div className="text-sm text-navy-900">{order.comments || 'Đơn mẫu (mock)'}</div>
          </div>
        </div>

        <div className="overflow-x-auto -mx-5 px-5">
          <table className="w-full text-sm min-w-[800px]">
            <thead>
              <tr className="text-xs text-slate-500 border-y bg-slate-50/50">
                <th className="text-left px-3 py-2 font-medium w-8">#</th>
                <th className="text-left px-3 py-2 font-medium">Hàng hoá</th>
                <th className="text-right px-3 py-2 font-medium">SL</th>
                <th className="text-right px-3 py-2 font-medium">Đơn giá</th>
                <th className="text-right px-3 py-2 font-medium">CK%</th>
                <th className="text-left px-3 py-2 font-medium">Kho</th>
                <th className="text-left px-3 py-2 font-medium">Ngày giao</th>
                <th className="text-left px-3 py-2 font-medium">Trạng thái dòng</th>
              </tr>
            </thead>
            <tbody>
              {lines.map((line, i) => (
                <tr key={i} className="border-b last:border-0">
                  <td className="px-3 py-2.5 text-slate-500">{i + 1}</td>
                  <td className="px-3 py-2.5">
                    <div className="font-medium text-navy-900">{line.description}</div>
                    <div className="text-xs text-slate-400">{line.item_code}</div>
                  </td>
                  <td className="px-3 py-2.5 text-right">{line.quantity}</td>
                  <td className="px-3 py-2.5 text-right">{(line.price || 0).toLocaleString('vi-VN')}</td>
                  <td className="px-3 py-2.5 text-right">{line.discount_percent || 0}</td>
                  <td className="px-3 py-2.5">{line.warehouse || '01'}</td>
                  <td className="px-3 py-2.5">{order.doc_date}</td>
                  <td className="px-3 py-2.5">
                    <span className="bg-blue-100 text-blue-700 text-xs px-2 py-0.5 rounded-full">
                      Đang mở
                    </span>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>

        <div className="flex justify-end mt-4">
          <div className="text-sm text-right">
            <div className="flex justify-between gap-8 text-slate-600 py-1">
              <span>Tổng sau CK dòng:</span>
              <span>{subtotal.toLocaleString('vi-VN')} VND</span>
            </div>
            <div className="flex justify-between gap-8 text-slate-600 py-1">
              <span>Chiết khấu đơn hàng ({order.discount_percent || 0}%):</span>
              <span>-{docDisc.toLocaleString('vi-VN')} VND</span>
            </div>
            <div className="mt-2 inline-block bg-navy-900 text-white font-bold px-6 py-2 rounded text-sm">
              TỔNG CỘNG: {(order.doc_total || 0).toLocaleString('vi-VN')} VND
            </div>
          </div>
        </div>
      </div>

      <Link href="/" className="text-sm text-slate-500 hover:text-navy-900">
        ← Về danh sách
      </Link>
    </Layout>
  );
}

export default function OrderDetail() {
  return (
    <Suspense
      fallback={
        <Layout title="Chi tiết đơn">
          <div className="text-center py-12 text-sm">Đang tải...</div>
        </Layout>
      }
    >
      <OrderDetailInner />
    </Suspense>
  );
}
