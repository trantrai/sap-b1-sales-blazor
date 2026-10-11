// app/settings/page.tsx — Cài đặt kết nối Service Layer.
'use client';

import { useState, useEffect } from 'react';
import Layout from '@/components/Layout';
import api from '@/lib/api-client';

interface DbEntry {
  code: string;
  name: string;
  isDefault?: boolean;
}

export default function Settings() {
  const [mode, setMode] = useState('demo');
  const [b1Url, setB1Url] = useState('https://your-b1-server:50000/b1s/v1');
  const [companyDb, setCompanyDb] = useState('SBODEMO_VN');
  const [b1Username, setB1Username] = useState('manager');
  const [b1Password, setB1Password] = useState('');
  const [timeout, setTimeout] = useState<number | string>(30);
  const [sslVerify, setSslVerify] = useState(true);
  const [databases, setDatabases] = useState<DbEntry[]>([
    { code: 'SBODEMO_VN', name: 'Demo VN', isDefault: true },
    { code: 'TESTDB', name: 'Test nội bộ', isDefault: false },
  ]);
  const [newDbCode, setNewDbCode] = useState('');
  const [newDbName, setNewDbName] = useState('');
  const [salesDiscount, setSalesDiscount] = useState<number | string>(10);
  const [managerDiscount, setManagerDiscount] = useState<number | string>(20);
  const [saving, setSaving] = useState(false);
  const [message, setMessage] = useState('');

  useEffect(() => {
    api
      .get<{ b1_mode?: string; b1_url?: string }>('/settings')
      .then(({ data }) => {
        if (data.b1_mode) setMode(data.b1_mode);
        if (data.b1_url) setB1Url(data.b1_url);
      })
      .catch(() => {});
    api
      .get<{ databases: DbEntry[] }>('/settings/databases')
      .then(({ data }) => {
        if (data.databases?.length) setDatabases(data.databases);
      })
      .catch(() => {});
  }, []);

  const handleSave = async () => {
    setSaving(true);
    setMessage('');
    try {
      await api.put('/settings', {
        b1_mode: mode,
        b1_url: b1Url,
        b1_company_db: companyDb,
        b1_username: b1Username,
        ...(b1Password && { b1_password: b1Password }),
        b1_timeout: timeout,
        b1_ssl_verify: sslVerify,
      });
      await api.put('/settings/databases', { databases });
      setMessage('Đã lưu và áp dụng ngay');
    } catch {
      setMessage('Lưu thất bại');
    } finally {
      setSaving(false);
    }
  };

  const handleTestConnection = async () => {
    try {
      const { data } = await api.post<{ success: boolean; message?: string }>(
        '/settings/test-connection'
      );
      alert(data.success ? 'Kết nối thành công' : `Thất bại: ${data.message}`);
    } catch (err) {
      const ae = err as { response?: { data?: { detail?: string; message?: string } } };
      alert(ae.response?.data?.detail || ae.response?.data?.message || 'Kiểm tra thất bại');
    }
  };

  const addDatabase = () => {
    if (!newDbCode.trim()) return;
    setDatabases([
      ...databases,
      {
        code: newDbCode.trim().toUpperCase(),
        name: newDbName.trim() || newDbCode.trim(),
        isDefault: false,
      },
    ]);
    setNewDbCode('');
    setNewDbName('');
  };

  const removeDatabase = (code: string) => {
    setDatabases(databases.filter((d) => d.code !== code));
  };

  const inputCls =
    'w-full px-3 py-2 text-sm border border-slate-300 rounded focus:outline-none focus:border-brand-blue';
  const labelCls = 'text-xs font-medium text-slate-600 mb-1 block';

  return (
    <Layout title="Cài đặt kết nối">
      <div className="bg-white rounded-lg border border-slate-200 p-5 mb-4">
        <h3 className="font-semibold text-navy-900 text-sm mb-2">Cài đặt kết nối Service Layer</h3>
        <p className="text-xs text-slate-500 leading-relaxed">
          Chọn chạy demo bằng dữ liệu giả hoặc kết nối tới SAP Business One qua Service Layer. Thay
          đổi được áp dụng ngay không cần khởi động lại server. Cấu hình hiện tại đang lấy từ cơ sở
          dữ liệu (ghi đè file .env).
        </p>
      </div>

      <div className="bg-white rounded-lg border border-slate-200 p-5 mb-4">
        <h4 className="font-semibold text-navy-900 text-sm mb-3">Chế độ kết nối</h4>
        <div className="flex gap-6">
          <label className="flex items-center gap-2 text-sm cursor-pointer">
            <input
              type="radio"
              checked={mode === 'demo'}
              onChange={() => setMode('demo')}
              className="accent-navy-800"
            />
            Demo (dữ liệu giả)
          </label>
          <label className="flex items-center gap-2 text-sm cursor-pointer">
            <input
              type="radio"
              checked={mode === 'live'}
              onChange={() => setMode('live')}
              className="accent-navy-800"
            />
            SAP B1 thật
          </label>
        </div>
        <p className="text-xs text-slate-400 mt-2">Thay đổi qua giao diện sẽ tự áp dụng ngay</p>
      </div>

      <div className="bg-white rounded-lg border border-slate-200 p-5 mb-4">
        <h4 className="font-semibold text-navy-900 text-sm mb-1">Thông tin Service Layer</h4>
        <p className="text-xs text-slate-500 mb-4">Cấu hình sử dụng cho chọn chế độ "SAP B1 thật"</p>
        <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-3 mb-3">
          <div>
            <label className={labelCls}>Service Layer URL</label>
            <input
              type="text"
              value={b1Url}
              onChange={(e) => setB1Url(e.target.value)}
              className={inputCls}
            />
          </div>
          <div>
            <label className={labelCls}>CompanyDB (mặc định khi đăng nhập)</label>
            <select
              value={companyDb}
              onChange={(e) => setCompanyDb(e.target.value)}
              className={`${inputCls} bg-white`}
            >
              {databases.map((d) => (
                <option key={d.code} value={d.code}>
                  {d.name} ({d.code})
                </option>
              ))}
            </select>
            <p className="text-xs text-slate-400 mt-1">
              Chọn từ danh sách database đã khai báo bên dưới
            </p>
          </div>
          <div>
            <label className={labelCls}>Tên đăng nhập (kỹ thuật)</label>
            <input
              type="text"
              value={b1Username}
              onChange={(e) => setB1Username(e.target.value)}
              className={inputCls}
            />
          </div>
          <div>
            <label className={labelCls}>Mật khẩu</label>
            <input
              type="password"
              value={b1Password}
              onChange={(e) => setB1Password(e.target.value)}
              className={inputCls}
            />
            <p className="text-xs text-slate-400 mt-1">
              Để trống nếu giữ nguyên mật khẩu đã lưu (được mã hoá trong DB)
            </p>
          </div>
          <div>
            <label className={labelCls}>Timeout (giây)</label>
            <input
              type="number"
              value={timeout}
              onChange={(e) => setTimeout(e.target.value)}
              className={inputCls}
            />
          </div>
          <div>
            <label className={labelCls}>Kiểm tra chứng chỉ SSL</label>
            <select
              value={String(sslVerify)}
              onChange={(e) => setSslVerify(e.target.value === 'true')}
              className={`${inputCls} bg-white`}
            >
              <option value="true">Bật (khuyến nghị)</option>
              <option value="false">Tắt</option>
            </select>
          </div>
        </div>
        <div className="bg-blue-50 border border-blue-200 rounded p-3 text-xs text-blue-700">
          Nên dùng tài khoản kỹ thuật (service account) riêng cho tích hợp, không dùng tài khoản
          manager cá nhân. Mật khẩu được mã hoá trước khi lưu vào DB local.
        </div>
      </div>

      <div className="bg-white rounded-lg border border-slate-200 p-5 mb-4">
        <h4 className="font-semibold text-navy-900 text-sm mb-1">Danh sách database truy cập</h4>
        <p className="text-xs text-slate-500 mb-4">
          Khai báo các CompanyDB SAP B1 được phép truy cập. Danh sách này hiện ở màn Đăng nhập để
          user chọn database làm việc.
        </p>
        <table className="w-full text-sm mb-4">
          <thead>
            <tr className="text-xs text-slate-500 border-b">
              <th className="text-left py-2 font-medium">Mã database</th>
              <th className="text-left py-2 font-medium">Tên hiển thị</th>
              <th className="text-right py-2 font-medium"></th>
            </tr>
          </thead>
          <tbody>
            {databases.map((d) => (
              <tr key={d.code} className="border-b last:border-0">
                <td className="py-2.5 font-mono text-xs">
                  {d.code} {d.isDefault && <span className="text-slate-400">(mặc định)</span>}
                </td>
                <td className="py-2.5">{d.name}</td>
                <td className="py-2.5 text-right">
                  <button
                    onClick={() => removeDatabase(d.code)}
                    className="text-sm text-slate-500 hover:text-red-600"
                  >
                    Xoá
                  </button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
        <div className="grid grid-cols-1 sm:grid-cols-2 gap-3 mb-3">
          <div>
            <label className={labelCls}>Mã database mới</label>
            <input
              type="text"
              value={newDbCode}
              onChange={(e) => setNewDbCode(e.target.value)}
              placeholder="VD: SBODEMO_VN"
              className={inputCls}
            />
          </div>
          <div>
            <label className={labelCls}>Tên hiển thị</label>
            <input
              type="text"
              value={newDbName}
              onChange={(e) => setNewDbName(e.target.value)}
              placeholder="VD: Demo VN"
              className={inputCls}
            />
          </div>
        </div>
        <div className="flex gap-2">
          <button
            onClick={addDatabase}
            className="px-4 py-2 text-sm border border-slate-300 rounded hover:bg-slate-50"
          >
            + Thêm database
          </button>
          <button
            onClick={handleSave}
            className="px-4 py-2 text-sm border border-slate-300 rounded hover:bg-slate-50"
          >
            💾 Lưu danh sách database
          </button>
        </div>
        <p className="text-xs text-slate-400 mt-2">
          Database mặc định khi hệ thống (CompanyDB B1) phải nằm trong danh sách này.
        </p>
      </div>

      <div className="bg-white rounded-lg border border-slate-200 p-5 mb-4">
        <h4 className="font-semibold text-navy-900 text-sm mb-1">Ngưỡng chiết khấu theo vai trò</h4>
        <p className="text-xs text-slate-500 mb-4">
          Chiết khấu tối đa (%) mỗi vai trò được áp cho đơn hàng. Vai trò admin không giới hạn.
        </p>
        <div className="grid grid-cols-1 sm:grid-cols-2 gap-3 mb-3">
          <div>
            <label className={labelCls}>Sales tối đa (%)</label>
            <input
              type="number"
              value={salesDiscount}
              onChange={(e) => setSalesDiscount(e.target.value)}
              className={inputCls}
            />
          </div>
          <div>
            <label className={labelCls}>Manager tối đa (%)</label>
            <input
              type="number"
              value={managerDiscount}
              onChange={(e) => setManagerDiscount(e.target.value)}
              className={inputCls}
            />
          </div>
        </div>
        <button className="px-4 py-2 text-sm border border-slate-300 rounded hover:bg-slate-50">
          💾 Lưu ngưỡng chiết khấu
        </button>
      </div>

      <div className="flex gap-2">
        <button
          onClick={handleTestConnection}
          className="px-4 py-2.5 text-sm border border-slate-300 rounded hover:bg-slate-50 bg-white"
        >
          🔧 Kiểm tra kết nối
        </button>
        <button
          onClick={handleSave}
          disabled={saving}
          className="px-6 py-2.5 bg-navy-800 text-white text-sm font-medium rounded hover:bg-navy-900 disabled:opacity-50"
        >
          {saving ? 'Đang lưu...' : '💾 Lưu & Áp dụng ngay'}
        </button>
      </div>
      {message && (
        <div className="mt-3 text-sm px-4 py-2.5 rounded bg-blue-50 text-blue-700 inline-block">
          {message}
        </div>
      )}
    </Layout>
  );
}
