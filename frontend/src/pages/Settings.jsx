import { useState, useEffect } from 'react';
import Layout from '../components/Layout';
import api from '../api/client';

export default function Settings() {
  const [settings, setSettings] = useState({});
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [message, setMessage] = useState('');

  useEffect(() => {
    api.get('/settings')
      .then(({ data }) => setSettings(data))
      .catch(() => {})
      .finally(() => setLoading(false));
  }, []);

  const handleSave = async (e) => {
    e.preventDefault();
    setSaving(true);
    setMessage('');
    try {
      await api.put('/settings', settings);
      setMessage('Đã lưu cài đặt');
    } catch {
      setMessage('Lưu thất bại');
    } finally {
      setSaving(false);
    }
  };

  if (loading) {
    return <Layout title="Cài đặt"><div className="text-center py-12">Đang tải…</div></Layout>;
  }

  return (
    <Layout title="Cài đặt">
      <form onSubmit={handleSave} className="card max-w-2xl">
        <h3 className="font-bold text-navy-900 mb-4">Kết nối SAP B1</h3>
        <div className="space-y-4">
          <div>
            <label className="form-label">Service Layer URL</label>
            <input
              type="text"
              value={settings.b1_url || ''}
              onChange={(e) => setSettings({ ...settings, b1_url: e.target.value })}
              className="input-field"
              placeholder="https://server:50000/b1s/v1"
            />
          </div>
          <div>
            <label className="form-label">Company DB</label>
            <input
              type="text"
              value={settings.b1_company_db || ''}
              onChange={(e) => setSettings({ ...settings, b1_company_db: e.target.value })}
              className="input-field"
            />
          </div>
          <div>
            <label className="form-label">Username</label>
            <input
              type="text"
              value={settings.b1_username || ''}
              onChange={(e) => setSettings({ ...settings, b1_username: e.target.value })}
              className="input-field"
            />
          </div>
        </div>
        {message && (
          <div className="mt-4 px-4 py-3 rounded-[10px] text-sm bg-blue-50 text-blue-700">
            {message}
          </div>
        )}
        <button type="submit" disabled={saving} className="btn-primary mt-6">
          {saving ? 'Đang lưu…' : '💾 Lưu cài đặt'}
        </button>
      </form>
    </Layout>
  );
}
