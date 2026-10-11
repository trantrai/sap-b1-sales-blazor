import { useState, useEffect } from 'react';
import { useParams, useNavigate, Link } from 'react-router-dom';
import Layout from '../components/Layout';
import api from '../api/client';

export default function OrderDetail() {
  const { id } = useParams();
  const navigate = useNavigate();
  const [order, setOrder] = useState(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    api.get(`/orders/${id}`)
      .then(({ data }) => setOrder(data))
      .catch(() => {})
      .finally(() => setLoading(false));
  }, [id]);

  const handleAction = async (action, confirmMsg) => {
    if (!confirm(confirmMsg)) return;
    try {
      await api.post(`/orders/${id}/${action}`);
      const { data } = await api.get(`/orders/${id}`);
      setOrder(data);
    } catch (err) {
      alert(err.response?.data?.message || 'Thất bại');
    }
  };

  if (loading) {
    return <Layout title="Chi tiết đơn"><div className="text-center py-12 text-sm text-slate-500">Đang tải...</div></Layout>;
  }
  if (!order) {
    return <Layout title="Chi tiết đơn"><div className="text-center py-12 text-sm text-slate-500">Không tìm thấy đơn hàng</div></Layout>;
  }

  const infoItems = [
    { label: 'Khách hàng', value: <><div className="font-medium">{order.card_name}</div><div className="text-xs text-slate-400">{order.card_code}</div></> },
    { label: 'Số PO của KH', value: order.num_at_card || 'PO-2026-001' },
    { label: 'Ngày chứng từ', value: order.doc_date },
    { label: 'Ngày giao hàng', value: order.delivery_date || order.doc_date },
    { label: 'Ngày hạch toán', value: order.tax_date || order.doc_date },
    { label: 'Tiền tệ / Tỷ giá', value: order.currency || 'VND' },
    { label: 'Điều khoản thanh toán', value: order.payment_terms || 'Công nợ 30 ngày' },
    { label: 'Vận chuyển', value: order.shipping_type || 'Giao hàng tiêu chuẩn' },
    { label: 'Kho nhận hàng', value: order.warehouse || '' },
    { label: 'Hình thức vận chuyển', value: 'Trụ sở' },
    { label: 'Dự án', value: order.project_code || '—' },
    { label: 'Chiết khấu đơn hàng', value: `${order.doc_discount || 0}%` },
  ];

  const subtotal = (order.lines || []).reduce((s, l) => s + (l.quantity || 0) * (l.price || 0), 0);
  const docDisc = subtotal * ((order.doc_discount || 0) / 100);

  return (
    <Layout title="Chi tiết đơn">
      <div className="bg-white rounded-lg border border-slate-200 p-5 mb-4">
        <div className="flex flex-wrap items-center gap-2 mb-5">
          <Link to="/" className="text-sm text-slate-500 hover:text-navy-900">← Danh sách</Link>
          <span className="font-semibold text-navy-900">Đơn hàng #{order.doc_num}</span>
          <span className="bg-blue-100 text-blue-700 text-xs px-2.5 py-0.5 rounded-full font-medium">
            {order.status === 'open' ? 'Đang mở' : order.status}
          </span>
          <div className="ml-auto flex flex-wrap gap-2">
            <button className="px-3 py-1.5 text-sm border border-slate-300 rounded hover:bg-slate-50 flex items-center gap-1.5">
              ✏️ Sửa ghi chú
            </button>
            <button onClick={() => navigate(`/orders/new?copy=${id}`)} className="px-3 py-1.5 text-sm border border-slate-300 rounded hover:bg-slate-50 flex items-center gap-1.5">
              📋 Copy thành đơn mới
            </button>
            <button onClick={() => window.print()} className="px-3 py-1.5 text-sm border border-slate-300 rounded hover:bg-slate-50 flex items-center gap-1.5">
              🖨️ In
            </button>
            {order.status === 'open' && (
              <>
                <button onClick={() => handleAction('close', 'Đóng đơn hàng này?')}
                  className="px-3 py-1.5 text-sm border border-slate-300 rounded hover:bg-slate-50 flex items-center gap-1.5">
                  ☑ Đóng đơn
                </button>
                <button onClick={() => handleAction('cancel', 'Hủy đơn hàng này?')}
                  className="px-3 py-1.5 text-sm bg-brand-orange text-white rounded hover:bg-red-700 flex items-center gap-1.5">
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
            <div className="text-sm font-bold text-navy-900">{(order.doc_total || 0).toLocaleString('vi-VN')} VND</div>
          </div>
          <div>
            <div className="text-xs text-slate-400 mb-0.5">Ghi chú</div>
            <div className="text-sm text-navy-900">{order.notes || 'Đơn mẫu (mock)'}</div>
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
              {(order.lines || []).map((line, i) => (
                <tr key={i} className="border-b last:border-0">
                  <td className="px-3 py-2.5 text-slate-500">{i + 1}</td>
                  <td className="px-3 py-2.5">
                    <div className="font-medium text-navy-900">{line.item_name}</div>
                    <div className="text-xs text-slate-400">{line.item_code}</div>
                  </td>
                  <td className="px-3 py-2.5 text-right">{line.quantity}</td>
                  <td className="px-3 py-2.5 text-right">{(line.price || 0).toLocaleString('vi-VN')}</td>
                  <td className="px-3 py-2.5 text-right">{line.discount || 0}</td>
                  <td className="px-3 py-2.5">{line.warehouse || '01'}</td>
                  <td className="px-3 py-2.5">{line.delivery_date || order.doc_date}</td>
                  <td className="px-3 py-2.5">
                    <span className="bg-blue-100 text-blue-700 text-xs px-2 py-0.5 rounded-full">Đang mở</span>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>

        <div className="flex justify-end mt-4">
          <div className="text-sm text-right">
            <div className="flex justify-between gap-8 text-slate-600 py-1">
              <span>Tổng sau CK dòng:</span><span>{subtotal.toLocaleString('vi-VN')} VND</span>
            </div>
            <div className="flex justify-between gap-8 text-slate-600 py-1">
              <span>Chiết khấu đơn hàng ({order.doc_discount || 0}%):</span><span>-{docDisc.toLocaleString('vi-VN')} VND</span>
            </div>
            <div className="mt-2 inline-block bg-navy-900 text-white font-bold px-6 py-2 rounded text-sm">
              TỔNG CỘNG: {(order.doc_total || 0).toLocaleString('vi-VN')} VND
            </div>
          </div>
        </div>
      </div>

      <Link to="/" className="text-sm text-slate-500 hover:text-navy-900">← Về danh sách</Link>
    </Layout>
  );
}
