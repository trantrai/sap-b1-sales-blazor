import { useState, useEffect } from 'react';
import { useNavigate, useSearchParams } from 'react-router-dom';
import Layout from '../components/Layout';
import api from '../api/client';

export default function OrderCreate() {
  const navigate = useNavigate();
  const [searchParams] = useSearchParams();
  const copyId = searchParams.get('copy');

  const [cardCode, setCardCode] = useState('');
  const [customers, setCustomers] = useState([]);
  const [customerSearch, setCustomerSearch] = useState('');
  const [items, setItems] = useState([]);
  const [itemSearch, setItemSearch] = useState('');
  const [lines, setLines] = useState([]);
  const [saving, setSaving] = useState(false);

  useEffect(() => {
    if (copyId) {
      api.get(`/orders/${copyId}`).then(({ data }) => {
        setCardCode(data.card_code);
        setLines((data.lines || []).map((l) => ({
          item_code: l.item_code,
          item_name: l.item_name,
          quantity: l.quantity,
          price: l.price,
        })));
      }).catch(() => {});
    }
  }, [copyId]);

  const searchCustomers = async (q) => {
    setCustomerSearch(q);
    if (q.length < 2) { setCustomers([]); return; }
    try {
      const { data } = await api.get('/masterdata/customers', { params: { search: q } });
      setCustomers(data.items || data || []);
    } catch { setCustomers([]); }
  };

  const searchItems = async (q) => {
    setItemSearch(q);
    if (q.length < 2) { setItems([]); return; }
    try {
      const { data } = await api.get('/masterdata/items', { params: { search: q } });
      setItems(data.items || data || []);
    } catch { setItems([]); }
  };

  const addLine = (item) => {
    setLines([...lines, {
      item_code: item.item_code,
      item_name: item.item_name,
      quantity: 1,
      price: item.price || 0,
    }]);
    setItemSearch('');
    setItems([]);
  };

  const updateLine = (i, field, value) => {
    const next = [...lines];
    next[i][field] = value;
    setLines(next);
  };

  const removeLine = (i) => setLines(lines.filter((_, x) => x !== i));

  const total = lines.reduce((s, l) => s + (l.quantity || 0) * (l.price || 0), 0);

  const handleSubmit = async (e) => {
    e.preventDefault();
    if (!cardCode || lines.length === 0) {
      alert('Vui lòng chọn khách hàng và thêm ít nhất 1 dòng hàng');
      return;
    }
    setSaving(true);
    try {
      const { data } = await api.post('/orders', {
        card_code: cardCode,
        lines: lines.map((l) => ({
          item_code: l.item_code,
          quantity: Number(l.quantity),
          price: Number(l.price),
        })),
      });
      navigate(`/orders/${data.doc_entry}`);
    } catch (err) {
      alert(err.response?.data?.message || 'Tạo đơn thất bại');
    } finally {
      setSaving(false);
    }
  };

  return (
    <Layout title={copyId ? 'Copy đơn hàng' : 'Tạo đơn hàng'}>
      <form onSubmit={handleSubmit}>
        <div className="card mb-4">
          <h3 className="font-bold text-navy-900 mb-4">Thông tin chung</h3>
          <div className="relative">
            <label className="form-label">Khách hàng *</label>
            <input
              type="text"
              value={customerSearch || cardCode}
              onChange={(e) => searchCustomers(e.target.value)}
              placeholder="Tìm theo mã hoặc tên KH…"
              className="input-field"
            />
            {customers.length > 0 && (
              <div className="absolute z-10 w-full bg-white border rounded-[10px] shadow-lg mt-1 max-h-60 overflow-y-auto">
                {customers.map((c) => (
                  <button
                    key={c.card_code}
                    type="button"
                    onClick={() => {
                      setCardCode(c.card_code);
                      setCustomerSearch(`${c.card_code} - ${c.card_name}`);
                      setCustomers([]);
                    }}
                    className="w-full text-left px-4 py-2 hover:bg-slate-50 text-sm"
                  >
                    <div className="font-medium">{c.card_code}</div>
                    <div className="text-slate-500 text-xs">{c.card_name}</div>
                  </button>
                ))}
              </div>
            )}
          </div>
        </div>

        <div className="card mb-4">
          <h3 className="font-bold text-navy-900 mb-4">Dòng hàng</h3>
          <div className="relative mb-4">
            <input
              type="text"
              value={itemSearch}
              onChange={(e) => searchItems(e.target.value)}
              placeholder="Tìm mặt hàng để thêm…"
              className="input-field"
            />
            {items.length > 0 && (
              <div className="absolute z-10 w-full bg-white border rounded-[10px] shadow-lg mt-1 max-h-60 overflow-y-auto">
                {items.map((item) => (
                  <button
                    key={item.item_code}
                    type="button"
                    onClick={() => addLine(item)}
                    className="w-full text-left px-4 py-2 hover:bg-slate-50 text-sm"
                  >
                    <div className="font-medium">{item.item_code}</div>
                    <div className="text-slate-500 text-xs">{item.item_name}</div>
                  </button>
                ))}
              </div>
            )}
          </div>

          {lines.length === 0 ? (
            <div className="text-center py-8 text-slate-400 text-sm">
              Chưa có dòng hàng nào. Tìm và thêm mặt hàng ở trên.
            </div>
          ) : (
            <div className="space-y-3">
              {lines.map((line, i) => (
                <div key={i} className="border rounded-[10px] p-3 bg-slate-50">
                  <div className="flex justify-between items-start mb-2">
                    <div>
                      <div className="font-semibold text-sm">{line.item_code}</div>
                      <div className="text-xs text-slate-500">{line.item_name}</div>
                    </div>
                    <button
                      type="button"
                      onClick={() => removeLine(i)}
                      className="text-red-500 hover:text-red-700 text-lg"
                    >
                      ✕
                    </button>
                  </div>
                  <div className="grid grid-cols-3 gap-2">
                    <div>
                      <label className="form-label">SL</label>
                      <input
                        type="number"
                        min="1"
                        value={line.quantity}
                        onChange={(e) => updateLine(i, 'quantity', e.target.value)}
                        className="input-field"
                      />
                    </div>
                    <div>
                      <label className="form-label">Đơn giá</label>
                      <input
                        type="number"
                        min="0"
                        value={line.price}
                        onChange={(e) => updateLine(i, 'price', e.target.value)}
                        className="input-field"
                      />
                    </div>
                    <div>
                      <label className="form-label">Thành tiền</label>
                      <div className="py-2.5 font-semibold text-sm">
                        {((line.quantity || 0) * (line.price || 0)).toLocaleString('vi-VN')}
                      </div>
                    </div>
                  </div>
                </div>
              ))}
            </div>
          )}

          <div className="mt-4 pt-4 border-t flex justify-between items-center">
            <span className="font-semibold text-navy-900">Tổng cộng:</span>
            <span className="text-xl font-bold text-brand-blue">
              {total.toLocaleString('vi-VN')} ₫
            </span>
          </div>
        </div>

        <div className="flex gap-3">
          <button type="submit" disabled={saving} className="btn-primary flex-1">
            {saving ? 'Đang lưu…' : '💾 Lưu đơn hàng'}
          </button>
          <button type="button" onClick={() => navigate('/')} className="btn-secondary">
            Hủy
          </button>
        </div>
      </form>
    </Layout>
  );
}
