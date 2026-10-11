// app/orders/new/page.tsx — Tạo đơn hàng mới.
'use client';

import { useState, useEffect, Suspense } from 'react';
import { useRouter, useSearchParams } from 'next/navigation';
import Layout from '@/components/Layout';
import api from '@/lib/api-client';
import type { OrderOut } from '@/lib/b1-client';


interface Line {
  item_code: string;
  item_name: string;
  quantity: number | string;
  price: number | string;
  discount: number | string;
  warehouse: string;
  delivery_date: string;
  tax_code: string;
}

interface Customer {
  card_code: string;
  card_name: string;
}

interface Item {
  item_code: string;
  item_name: string;
  price: number;
}

const emptyLine = (): Line => ({
  item_code: '',
  item_name: '',
  quantity: 1,
  price: 0,
  discount: 0,
  warehouse: '',
  delivery_date: '',
  tax_code: '',
});

function OrderCreateInner() {
  const router = useRouter();
  const searchParams = useSearchParams();
  const copyId = searchParams.get('copy');

  const [cardCode, setCardCode] = useState('');
  const [cardName, setCardName] = useState('');
  const [customerSearch, setCustomerSearch] = useState('');
  const [customers, setCustomers] = useState<Customer[]>([]);
  const [deliveryDate, setDeliveryDate] = useState('');
  const [docDiscount, setDocDiscount] = useState<number | string>(0);
  const [notes, setNotes] = useState('');

  const [lines, setLines] = useState<Line[]>([emptyLine()]);
  const [itemSearches, setItemSearches] = useState<string[]>(['']);
  const [itemResults, setItemResults] = useState<Item[][]>([[]]);

  const [numAtCard, setNumAtCard] = useState('');
  const [taxDate, setTaxDate] = useState('');
  const [currency, setCurrency] = useState('VND');
  const [exchangeRate, setExchangeRate] = useState('');
  const [paymentTerms, setPaymentTerms] = useState('');
  const [shippingType, setShippingType] = useState('');
  const [shipAddress, setShipAddress] = useState('');
  const [billAddress, setBillAddress] = useState('');
  const [projectCode, setProjectCode] = useState('');

  const [saving, setSaving] = useState(false);

  useEffect(() => {
    if (copyId) {
      api
        .get<OrderOut>(`/orders/${copyId}`)
        .then(({ data }) => {
          setCardCode(data.card_code);
          setCardName(data.card_name);
          setCustomerSearch(`${data.card_code} - ${data.card_name}`);
          const copied: Line[] = (data.lines || []).map((l) => ({
            item_code: l.item_code,
            item_name: l.description,
            quantity: l.quantity,
            price: l.price,
            discount: 0,
            warehouse: '',
            delivery_date: '',
            tax_code: '',
          }));
          setLines(copied.length ? copied : [emptyLine()]);
          setItemSearches(copied.map(() => ''));
          setItemResults(copied.map(() => []));
        })
        .catch(() => {});
    }
  }, [copyId]);

  const searchCustomers = async (q: string) => {
    setCustomerSearch(q);
    if (q.length < 2) {
      setCustomers([]);
      return;
    }
    try {
      const { data } = await api.get<{ items?: Customer[] }>('/masterdata/customers', {
        params: { search: q },
      });
      setCustomers(data.items || []);
    } catch {
      setCustomers([]);
    }
  };

  const searchItems = async (idx: number, q: string) => {
    const ns = [...itemSearches];
    ns[idx] = q;
    setItemSearches(ns);
    if (q.length < 2) {
      const nr = [...itemResults];
      nr[idx] = [];
      setItemResults(nr);
      return;
    }
    try {
      const { data } = await api.get<{ items?: Item[] }>('/masterdata/items', {
        params: { search: q },
      });
      const nr = [...itemResults];
      nr[idx] = data.items || [];
      setItemResults(nr);
    } catch {
      const nr = [...itemResults];
      nr[idx] = [];
      setItemResults(nr);
    }
  };

  const selectItem = (idx: number, item: Item) => {
    const nl = [...lines];
    nl[idx] = {
      ...nl[idx],
      item_code: item.item_code,
      item_name: item.item_name,
      price: item.price || 0,
    };
    setLines(nl);
    const ns = [...itemSearches];
    ns[idx] = '';
    setItemSearches(ns);
    const nr = [...itemResults];
    nr[idx] = [];
    setItemResults(nr);
  };

  const updateLine = (idx: number, field: keyof Line, value: string) => {
    const nl = [...lines];
    nl[idx] = { ...nl[idx], [field]: value };
    setLines(nl);
  };

  const addLine = () => {
    setLines([...lines, emptyLine()]);
    setItemSearches([...itemSearches, '']);
    setItemResults([...itemResults, []]);
  };

  const removeLine = (idx: number) => {
    if (lines.length <= 1) return;
    setLines(lines.filter((_, i) => i !== idx));
    setItemSearches(itemSearches.filter((_, i) => i !== idx));
    setItemResults(itemResults.filter((_, i) => i !== idx));
  };

  const lineTotal = (l: Line) =>
    (Number(l.quantity) || 0) * (Number(l.price) || 0) * (1 - (Number(l.discount) || 0) / 100);
  const subtotal = lines.reduce((s, l) => s + lineTotal(l), 0);
  const docDiscAmount = subtotal * (Number(docDiscount) / 100);
  const grandTotal = subtotal - docDiscAmount;

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!cardCode) {
      alert('Hãy chọn khách hàng ở mục 1');
      return;
    }
    const validLines = lines.filter((l) => l.item_code);
    if (!validLines.length) {
      alert('Hãy thêm ít nhất 1 dòng hàng');
      return;
    }
    setSaving(true);
    try {
      const { data } = await api.post<{ doc_entry: number }>('/orders', {
        card_code: cardCode,
        doc_discount: Number(docDiscount),
        notes,
        num_at_card: numAtCard,
        lines: validLines.map((l) => ({
          item_code: l.item_code,
          quantity: Number(l.quantity),
          price: Number(l.price),
          discount: Number(l.discount),
        })),
      });
      router.push(`/orders/${data.doc_entry}`);
    } catch (err) {
      const ae = err as { response?: { data?: { detail?: string; message?: string } } };
      alert(ae.response?.data?.detail || ae.response?.data?.message || 'Tạo đơn thất bại');
    } finally {
      setSaving(false);
    }
  };

  const inputCls =
    'w-full px-2.5 py-1.5 text-sm border border-slate-300 rounded focus:outline-none focus:border-brand-blue';
  const labelCls = 'text-xs font-medium text-slate-600 mb-1 block';

  return (
    <Layout title="Tạo đơn hàng">
      <form onSubmit={handleSubmit}>
        {/* Section 1: Khách hàng */}
        <div className="bg-white rounded-lg border border-slate-200 p-5 mb-4">
          <h3 className="font-semibold text-navy-900 text-sm mb-1">1. Khách hàng</h3>
          <p className="text-xs text-slate-500 mb-4">Chọn đối tác trong SAP B1</p>
          <div className="relative mb-4">
            <input
              type="text"
              value={customerSearch}
              onChange={(e) => searchCustomers(e.target.value)}
              placeholder="Gõ ít nhất 2 ký tự 🔍"
              className="w-full px-3 py-2 text-sm border border-slate-300 rounded focus:outline-none focus:border-brand-blue"
            />
            {customers.length > 0 && (
              <div className="absolute z-10 w-full bg-white border rounded shadow-lg mt-1 max-h-56 overflow-y-auto">
                {customers.map((c) => (
                  <button
                    key={c.card_code}
                    type="button"
                    onClick={() => {
                      setCardCode(c.card_code);
                      setCardName(c.card_name);
                      setCustomerSearch(`${c.card_code} - ${c.card_name}`);
                      setCustomers([]);
                    }}
                    className="w-full text-left px-3 py-2 hover:bg-slate-50 text-sm"
                  >
                    <div className="font-medium">{c.card_code}</div>
                    <div className="text-xs text-slate-500">{c.card_name}</div>
                  </button>
                ))}
              </div>
            )}
          </div>
          <div className="grid grid-cols-1 sm:grid-cols-3 gap-3">
            <div>
              <label className={labelCls}>Ngày giao hàng</label>
              <input
                type="date"
                value={deliveryDate}
                onChange={(e) => setDeliveryDate(e.target.value)}
                className={inputCls}
              />
            </div>
            <div>
              <label className={labelCls}>Chiết khấu đơn hàng (%)</label>
              <input
                type="number"
                min="0"
                max="100"
                value={docDiscount}
                onChange={(e) => setDocDiscount(e.target.value)}
                className={inputCls}
              />
            </div>
            <div>
              <label className={labelCls}>Ghi chú</label>
              <input
                type="text"
                value={notes}
                onChange={(e) => setNotes(e.target.value)}
                placeholder="Ghi chú đơn hàng..."
                className={inputCls}
              />
            </div>
          </div>
        </div>

        {/* Section 2: Dòng hàng */}
        <div className="bg-white rounded-lg border border-slate-200 p-5 mb-4">
          <h3 className="font-semibold text-navy-900 text-sm mb-1">2. Dòng hàng</h3>
          <p className="text-xs text-slate-500 mb-4">Thêm từng mặt hàng vào đơn</p>
          <div className="overflow-x-auto -mx-5 px-5">
            <table className="w-full text-sm min-w-[900px]">
              <thead>
                <tr className="text-xs text-slate-500 border-b">
                  <th className="text-left py-2 font-medium w-8">#</th>
                  <th className="text-left py-2 font-medium">Hàng hoá *</th>
                  <th className="text-left py-2 font-medium w-16">SL *</th>
                  <th className="text-left py-2 font-medium w-24">Đơn giá</th>
                  <th className="text-left py-2 font-medium w-16">CK %</th>
                  <th className="text-left py-2 font-medium w-24">Kho</th>
                  <th className="text-left py-2 font-medium w-28">Ngày giao</th>
                  <th className="text-left py-2 font-medium w-24">Thuế suất</th>
                  <th className="text-right py-2 font-medium w-24">Thành tiền</th>
                  <th className="w-8"></th>
                </tr>
              </thead>
              <tbody>
                {lines.map((line, idx) => (
                  <tr key={idx} className="border-b last:border-0">
                    <td className="py-2 text-slate-500">{idx + 1}</td>
                    <td className="py-2 pr-2">
                      <div className="relative">
                        <input
                          type="text"
                          value={line.item_code || itemSearches[idx]}
                          onChange={(e) => {
                            if (!line.item_code) searchItems(idx, e.target.value);
                            else {
                              updateLine(idx, 'item_code', '');
                              updateLine(idx, 'item_name', '');
                              searchItems(idx, e.target.value);
                            }
                          }}
                          placeholder="Gõ ít nhất 2 ký tự"
                          className={inputCls}
                        />
                        {line.item_name && (
                          <div className="text-xs text-slate-500 mt-0.5">{line.item_name}</div>
                        )}
                        {itemResults[idx]?.length > 0 && (
                          <div className="absolute z-10 w-64 bg-white border rounded shadow-lg mt-1 max-h-48 overflow-y-auto">
                            {itemResults[idx].map((item) => (
                              <button
                                key={item.item_code}
                                type="button"
                                onClick={() => selectItem(idx, item)}
                                className="w-full text-left px-3 py-2 hover:bg-slate-50 text-sm"
                              >
                                <div className="font-medium">{item.item_code}</div>
                                <div className="text-xs text-slate-500">{item.item_name}</div>
                              </button>
                            ))}
                          </div>
                        )}
                      </div>
                    </td>
                    <td className="py-2 pr-2">
                      <input
                        type="number"
                        min="1"
                        value={line.quantity}
                        onChange={(e) => updateLine(idx, 'quantity', e.target.value)}
                        className={inputCls}
                      />
                    </td>
                    <td className="py-2 pr-2">
                      <input
                        type="number"
                        min="0"
                        value={line.price}
                        onChange={(e) => updateLine(idx, 'price', e.target.value)}
                        className={inputCls}
                      />
                    </td>
                    <td className="py-2 pr-2">
                      <input
                        type="number"
                        min="0"
                        max="100"
                        value={line.discount}
                        onChange={(e) => updateLine(idx, 'discount', e.target.value)}
                        className={inputCls}
                      />
                    </td>
                    <td className="py-2 pr-2">
                      <select
                        value={line.warehouse}
                        onChange={(e) => updateLine(idx, 'warehouse', e.target.value)}
                        className={`${inputCls} bg-white`}
                      >
                        <option value="">—</option>
                        <option value="01">01</option>
                        <option value="02">02</option>
                      </select>
                    </td>
                    <td className="py-2 pr-2">
                      <input
                        type="date"
                        value={line.delivery_date}
                        onChange={(e) => updateLine(idx, 'delivery_date', e.target.value)}
                        className={inputCls}
                      />
                    </td>
                    <td className="py-2 pr-2">
                      <select
                        value={line.tax_code}
                        onChange={(e) => updateLine(idx, 'tax_code', e.target.value)}
                        className={`${inputCls} bg-white`}
                      >
                        <option value="">Mặc định</option>
                        <option value="VAT10">VAT 10%</option>
                        <option value="VAT0">VAT 0%</option>
                      </select>
                    </td>
                    <td className="py-2 text-right font-medium">
                      {lineTotal(line).toLocaleString('vi-VN')}
                    </td>
                    <td className="py-2 text-center">
                      <button
                        type="button"
                        onClick={() => removeLine(idx)}
                        className="text-red-400 hover:text-red-600"
                      >
                        ✕
                      </button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
          <button
            type="button"
            onClick={addLine}
            className="mt-3 text-sm text-navy-900 font-medium hover:underline"
          >
            + Thêm dòng hàng
          </button>
        </div>

        {/* Section 3: Thông tin bổ sung */}
        <div className="bg-white rounded-lg border border-slate-200 p-5 mb-4">
          <h3 className="font-semibold text-navy-900 text-sm mb-1">3. Thông tin bổ sung</h3>
          <p className="text-xs text-slate-500 mb-4">Các trường chuẩn SAP B1</p>
          <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-3">
            <div>
              <label className={labelCls}>Số PO của KH (NumAtCard)</label>
              <input
                type="text"
                value={numAtCard}
                onChange={(e) => setNumAtCard(e.target.value)}
                placeholder="VD: PO-2026-001"
                className={inputCls}
              />
            </div>
            <div>
              <label className={labelCls}>Ngày hạch toán (TaxDate)</label>
              <input
                type="date"
                value={taxDate}
                onChange={(e) => setTaxDate(e.target.value)}
                className={inputCls}
              />
            </div>
            <div>
              <label className={labelCls}>Tiền tệ</label>
              <select
                value={currency}
                onChange={(e) => setCurrency(e.target.value)}
                className={`${inputCls} bg-white`}
              >
                <option>VND</option>
                <option>USD</option>
              </select>
            </div>
            <div>
              <label className={labelCls}>Tỷ giá (nếu ngoại tệ)</label>
              <input
                type="text"
                value={exchangeRate}
                onChange={(e) => setExchangeRate(e.target.value)}
                placeholder="VD: 25000"
                className={inputCls}
              />
            </div>
            <div>
              <label className={labelCls}>Điều khoản thanh toán</label>
              <select
                value={paymentTerms}
                onChange={(e) => setPaymentTerms(e.target.value)}
                className={`${inputCls} bg-white`}
              >
                <option value="">Theo KH</option>
                <option>Công nợ 30 ngày</option>
              </select>
            </div>
            <div>
              <label className={labelCls}>Hình thức vận chuyển</label>
              <select
                value={shippingType}
                onChange={(e) => setShippingType(e.target.value)}
                className={`${inputCls} bg-white`}
              >
                <option value="">—</option>
                <option>Giao hàng tiêu chuẩn</option>
              </select>
            </div>
            <div>
              <label className={labelCls}>Địa chỉ giao hàng</label>
              <select
                value={shipAddress}
                onChange={(e) => setShipAddress(e.target.value)}
                className={`${inputCls} bg-white`}
              >
                <option value="">—</option>
              </select>
            </div>
            <div>
              <label className={labelCls}>Địa chỉ hoá đơn</label>
              <select
                value={billAddress}
                onChange={(e) => setBillAddress(e.target.value)}
                className={`${inputCls} bg-white`}
              >
                <option value="">—</option>
              </select>
            </div>
            <div>
              <label className={labelCls}>Mã dự án</label>
              <input
                type="text"
                value={projectCode}
                onChange={(e) => setProjectCode(e.target.value)}
                placeholder="VD: PRJ-01"
                className={inputCls}
              />
            </div>
          </div>
        </div>

        {/* Summary + Submit */}
        <div className="bg-white rounded-lg border border-slate-200 p-5 flex flex-wrap items-center justify-between gap-4">
          <div>
            <button
              type="submit"
              disabled={saving || !cardCode}
              className="px-6 py-2.5 bg-navy-800 text-white text-sm font-medium rounded hover:bg-navy-900 disabled:opacity-40 disabled:cursor-not-allowed flex items-center gap-2"
            >
              {saving ? 'Đang lưu...' : '✓ Tạo đơn hàng'}
            </button>
            {!cardCode && <p className="text-xs text-slate-400 mt-1.5">Hãy chọn khách hàng ở mục 1.</p>}
          </div>
          <div className="text-right text-sm">
            <div className="flex justify-between gap-8 text-slate-600">
              <span>Tổng sau CK dòng:</span>
              <span>{subtotal.toLocaleString('vi-VN')} VND</span>
            </div>
            <div className="flex justify-between gap-8 text-slate-600">
              <span>Chiết khấu đơn hàng ({docDiscount}%):</span>
              <span>-{docDiscAmount.toLocaleString('vi-VN')} VND</span>
            </div>
            <div className="mt-2 inline-block bg-navy-900 text-white font-bold px-6 py-2 rounded text-sm">
              TỔNG CỘNG: {grandTotal.toLocaleString('vi-VN')} VND
            </div>
          </div>
        </div>
      </form>
    </Layout>
  );
}

export default function OrderCreate() {
  return (
    <Suspense
      fallback={
        <Layout title="Tạo đơn hàng">
          <div className="text-center py-12 text-sm">Đang tải...</div>
        </Layout>
      }
    >
      <OrderCreateInner />
    </Suspense>
  );
}
