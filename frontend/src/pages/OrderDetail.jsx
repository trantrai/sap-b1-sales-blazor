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
      alert('Thành công');
      const { data } = await api.get(`/orders/${id}`);
      setOrder(data);
    } catch (err) {
      alert(err.response?.data?.message || 'Thất bại');
    }
  };

  const handleCopy = () => navigate(`/orders/new?copy=${id}`);

  if (loading) {
    return <Layout title="Chi tiết đơn"><div className="text-center py-12">Đang tải…</div></Layout>;
  }
  if (!order) {
    return <Layout title="Chi tiết đơn"><div className="text-center py-12">Không tìm thấy đơn hàng</div></Layout>;
  }

  return (
    <Layout title={`Đơn #${order.doc_num}`}>
      <div className="card mb-4">
        <div className="flex flex-wrap items-start justify-between gap-4 mb-6">
          <div>
            <h3 className="text-xl font-bold text-navy-900">Đơn hàng #{order.doc_num}</h3>
            <p className="text-slate-500 text-sm mt-1">Ngày: {order.doc_date}</p>
          </div>
          <div className="flex flex-wrap gap-2">
            <button onClick={handleCopy} className="btn-secondary">📋 Copy</button>
            <button onClick={() => window.print()} className="btn-secondary">🖨️ In</button>
            {order.status === 'open' && (
              <>
                <button
                  onClick={() => handleAction('close', 'Đóng đơn hàng này?')}
                  className="btn-secondary"
                >
                  🔒 Đóng
                </button>
                <button
                  onClick={() => handleAction('cancel', 'Hủy đơn hàng này?')}
                  className="btn-danger"
                >
                  ✕ Hủy
                </button>
              </>
            )}
          </div>
        </div>

        <div className="grid grid-cols-1 md:grid-cols-2 gap-4 mb-6">
          <div>
            <div className="form-label">Khách hàng</div>
            <div className="font-semibold">{order.card_name}</div>
            <div className="text-sm text-slate-500">{order.card_code}</div>
          </div>
          <div>
            <div className="form-label">Tổng tiền</div>
            <div className="text-2xl font-bold text-brand-blue">
              {(order.doc_total || 0).toLocaleString('vi-VN')} ₫
            </div>
          </div>
        </div>

        <h4 className="font-bold text-navy-900 mb-3">Chi tiết dòng hàng</h4>
        <div className="overflow-x-auto -mx-6">
          <table className="w-full text-sm min-w-[600px]">
            <thead className="bg-slate-50">
              <tr>
                <th className="text-left px-4 py-2">Mã hàng</th>
                <th className="text-left px-4 py-2">Tên hàng</th>
                <th className="text-right px-4 py-2">SL</th>
                <th className="text-right px-4 py-2">Đơn giá</th>
                <th className="text-right px-4 py-2">Thành tiền</th>
              </tr>
            </thead>
            <tbody>
              {(order.lines || []).map((line, i) => (
                <tr key={i} className="border-t">
                  <td className="px-4 py-2 font-medium">{line.item_code}</td>
                  <td className="px-4 py-2">{line.item_name}</td>
                  <td className="px-4 py-2 text-right">{line.quantity}</td>
                  <td className="px-4 py-2 text-right">{(line.price || 0).toLocaleString('vi-VN')}</td>
                  <td className="px-4 py-2 text-right font-semibold">
                    {((line.quantity || 0) * (line.price || 0)).toLocaleString('vi-VN')}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </div>

      <Link to="/" className="text-brand-blue hover:underline text-sm">← Về danh sách</Link>
    </Layout>
  );
}
