import { useState, useEffect, useCallback } from 'react';
import { Link, useSearchParams } from 'react-router-dom';
import Layout from '../components/Layout';
import api from '../api/client';

const statusBadge = (status) => {
  const map = {
    open: 'badge badge-open',
    closed: 'badge badge-closed',
    cancelled: 'badge badge-cancelled',
  };
  const labels = { open: 'Mở', closed: 'Đóng', cancelled: 'Hủy' };
  return <span className={map[status] || 'badge'}>{labels[status] || status}</span>;
};

export default function Orders() {
  const [searchParams, setSearchParams] = useSearchParams();
  const [orders, setOrders] = useState([]);
  const [total, setTotal] = useState(0);
  const [loading, setLoading] = useState(true);

  const status = searchParams.get('status') || '';
  const cardCode = searchParams.get('card_code') || '';
  const docNum = searchParams.get('doc_num') || '';
  const page = parseInt(searchParams.get('page') || '1');
  const pageSize = 20;

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const params = { page, page_size: pageSize };
      if (status) params.status = status;
      if (cardCode) params.card_code = cardCode;
      if (docNum) params.doc_num = docNum;
      const { data } = await api.get('/orders', { params });
      setOrders(data.items || data || []);
      setTotal(data.total || data.length || 0);
    } catch {
      setOrders([]);
    } finally {
      setLoading(false);
    }
  }, [status, cardCode, docNum, page]);

  useEffect(() => { load(); }, [load]);

  const updateFilter = (key, value) => {
    const next = new URLSearchParams(searchParams);
    if (value) next.set(key, value); else next.delete(key);
    next.set('page', '1');
    setSearchParams(next);
  };

  const totalPages = Math.ceil(total / pageSize);

  const handleExport = async () => {
    try {
      const params = {};
      if (status) params.status = status;
      if (cardCode) params.card_code = cardCode;
      if (docNum) params.doc_num = docNum;
      const { data } = await api.get('/orders/export', { params, responseType: 'blob' });
      const url = URL.createObjectURL(new Blob([data]));
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
      {/* Filter */}
      <div className="card mb-4">
        <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-3">
          <div>
            <label className="form-label">Trạng thái</label>
            <select
              value={status}
              onChange={(e) => updateFilter('status', e.target.value)}
              className="input-field"
            >
              <option value="">Tất cả</option>
              <option value="open">Mở</option>
              <option value="closed">Đóng</option>
              <option value="cancelled">Hủy</option>
            </select>
          </div>
          <div>
            <label className="form-label">Mã KH</label>
            <input
              type="text"
              value={cardCode}
              onChange={(e) => updateFilter('card_code', e.target.value)}
              placeholder="C20000"
              className="input-field"
            />
          </div>
          <div>
            <label className="form-label">Số đơn</label>
            <input
              type="text"
              value={docNum}
              onChange={(e) => updateFilter('doc_num', e.target.value)}
              placeholder="5001"
              className="input-field"
            />
          </div>
          <div className="flex items-end gap-2">
            <button onClick={load} className="btn-primary flex-1">🔍 Lọc</button>
            <button onClick={handleExport} className="btn-secondary" title="Xuất Excel">
              📥
            </button>
          </div>
        </div>
      </div>

      {/* Table */}
      <div className="card !p-0 overflow-hidden">
        {loading ? (
          <div className="text-center py-12 text-slate-500">Đang tải…</div>
        ) : orders.length === 0 ? (
          <div className="text-center py-12 text-slate-500">Không có đơn hàng</div>
        ) : (
          <div className="overflow-x-auto">
            <table className="w-full text-sm min-w-[860px]">
              <thead className="bg-slate-50 border-b">
                <tr>
                  <th className="text-left px-4 py-3 font-semibold text-navy-900">Số đơn</th>
                  <th className="text-left px-4 py-3 font-semibold text-navy-900">Khách hàng</th>
                  <th className="text-left px-4 py-3 font-semibold text-navy-900">Ngày</th>
                  <th className="text-right px-4 py-3 font-semibold text-navy-900">Tổng tiền</th>
                  <th className="text-left px-4 py-3 font-semibold text-navy-900">Trạng thái</th>
                  <th className="px-4 py-3"></th>
                </tr>
              </thead>
              <tbody>
                {orders.map((o) => (
                  <tr key={o.doc_entry} className="border-b hover:bg-slate-50">
                    <td className="px-4 py-3 font-semibold text-brand-blue">
                      <Link to={`/orders/${o.doc_entry}`}>{o.doc_num}</Link>
                    </td>
                    <td className="px-4 py-3">
                      <div className="font-medium">{o.card_name}</div>
                      <div className="text-xs text-slate-500">{o.card_code}</div>
                    </td>
                    <td className="px-4 py-3">{o.doc_date}</td>
                    <td className="px-4 py-3 text-right font-semibold">
                      {(o.doc_total || 0).toLocaleString('vi-VN')}
                    </td>
                    <td className="px-4 py-3">{statusBadge(o.status)}</td>
                    <td className="px-4 py-3 text-right">
                      <Link
                        to={`/orders/${o.doc_entry}`}
                        className="text-brand-blue hover:underline text-sm"
                      >
                        Chi tiết →
                      </Link>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </div>

      {/* Pagination */}
      {totalPages > 1 && (
        <div className="flex items-center justify-center gap-2 mt-4">
          <button
            disabled={page <= 1}
            onClick={() => updateFilter('page', String(page - 1))}
            className="btn-secondary !min-h-[36px] !px-3 disabled:opacity-50"
          >
            ←
          </button>
          <span className="text-sm text-slate-600">
            Trang {page} / {totalPages} ({total} đơn)
          </span>
          <button
            disabled={page >= totalPages}
            onClick={() => updateFilter('page', String(page + 1))}
            className="btn-secondary !min-h-[36px] !px-3 disabled:opacity-50"
          >
            →
          </button>
        </div>
      )}
    </Layout>
  );
}
