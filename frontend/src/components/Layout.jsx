import { NavLink, useNavigate } from 'react-router-dom';
import { useAuth } from '../context/AuthContext';
import { useState } from 'react';

const menuItems = [
  { to: '/dashboard', label: 'Dashboard', icon: '📊' },
  { to: '/', label: 'Danh sách đơn', icon: '📋' },
  { to: '/orders/new', label: 'Tạo đơn', icon: '➕' },
  { to: '/sync', label: 'Giám sát tích hợp', icon: '🔄' },
  { to: '/audit', label: 'Audit', icon: '📝' },
  { to: '/users', label: 'Người dùng', icon: '👥' },
  { to: '/settings', label: 'Cài đặt', icon: '⚙️' },
];

export default function Layout({ children, title }) {
  const { user, logout } = useAuth();
  const navigate = useNavigate();
  const [sidebarOpen, setSidebarOpen] = useState(false);
  const [dark, setDark] = useState(false);

  const handleLogout = async () => {
    await logout();
    navigate('/login');
  };

  return (
    <div className={`min-h-screen ${dark ? 'dark bg-slate-900' : 'bg-[#faf8f1]'}`}>
      {/* Sidebar - Desktop */}
      <aside className="hidden md:flex fixed inset-y-0 left-0 w-60 bg-navy-900 flex-col z-40">
        <div className="p-5 border-b border-white/10">
          <h1 className="text-white font-bold text-lg">SAP B1 Sales</h1>
          <p className="text-white/60 text-xs mt-1">Quản lý đơn hàng</p>
        </div>
        <nav className="flex-1 p-3 space-y-1 overflow-y-auto">
          {menuItems.map((item) => (
            <NavLink
              key={item.to}
              to={item.to}
              end={item.to === '/'}
              className={({ isActive }) =>
                `flex items-center gap-3 px-4 py-2.5 rounded-[10px] text-sm font-medium transition-colors ${
                  isActive
                    ? 'bg-brand-yellow text-navy-900'
                    : 'text-white/80 hover:bg-white/10 hover:text-white'
                }`
              }
            >
              <span className="text-lg">{item.icon}</span>
              {item.label}
            </NavLink>
          ))}
        </nav>
        <div className="p-4 border-t border-white/10">
          <div className="text-white/80 text-sm mb-2">
            <div className="font-semibold text-white">{user?.username}</div>
            <div className="text-xs text-white/60">{user?.role} • {user?.companyDb}</div>
          </div>
          <button
            onClick={handleLogout}
            className="w-full text-left px-4 py-2 rounded-[10px] text-sm text-white/80 hover:bg-white/10 transition-colors"
          >
            🚪 Đăng xuất
          </button>
        </div>
      </aside>

      {/* Sidebar - Mobile drawer */}
      {sidebarOpen && (
        <>
          <div
            className="fixed inset-0 bg-black/50 z-40 md:hidden"
            onClick={() => setSidebarOpen(false)}
          />
          <aside className="fixed inset-y-0 left-0 w-64 bg-navy-900 flex flex-col z-50 md:hidden">
            <div className="p-5 border-b border-white/10 flex justify-between items-center">
              <div>
                <h1 className="text-white font-bold text-lg">SAP B1 Sales</h1>
                <p className="text-white/60 text-xs mt-1">Quản lý đơn hàng</p>
              </div>
              <button
                onClick={() => setSidebarOpen(false)}
                className="text-white/80 hover:text-white text-2xl"
              >
                ✕
              </button>
            </div>
            <nav className="flex-1 p-3 space-y-1 overflow-y-auto">
              {menuItems.map((item) => (
                <NavLink
                  key={item.to}
                  to={item.to}
                  end={item.to === '/'}
                  onClick={() => setSidebarOpen(false)}
                  className={({ isActive }) =>
                    `flex items-center gap-3 px-4 py-2.5 rounded-[10px] text-sm font-medium transition-colors ${
                      isActive
                        ? 'bg-brand-yellow text-navy-900'
                        : 'text-white/80 hover:bg-white/10 hover:text-white'
                    }`
                  }
                >
                  <span className="text-lg">{item.icon}</span>
                  {item.label}
                </NavLink>
              ))}
            </nav>
            <div className="p-4 border-t border-white/10">
              <button
                onClick={handleLogout}
                className="w-full text-left px-4 py-2 rounded-[10px] text-sm text-white/80 hover:bg-white/10"
              >
                🚪 Đăng xuất ({user?.username})
              </button>
            </div>
          </aside>
        </>
      )}

      {/* Main content */}
      <div className="md:ml-60">
        {/* Topbar */}
        <header className="sticky top-0 z-30 bg-white border-b-2 border-brand-yellow">
          <div className="flex items-center gap-3 px-4 py-2">
            <button
              onClick={() => setSidebarOpen(true)}
              className="md:hidden p-2 rounded-lg hover:bg-slate-100 text-xl"
              aria-label="Mở menu"
            >
              ☰
            </button>
            <h2 className="text-lg font-bold text-navy-900 flex-1 truncate">{title}</h2>
            <button
              onClick={() => setDark(!dark)}
              className="p-2 rounded-lg hover:bg-slate-100 text-xl"
              aria-label="Chế độ tối/sáng"
            >
              {dark ? '☀️' : '🌙'}
            </button>
          </div>
          <div className="px-4 pb-3 md:hidden">
            <input
              type="search"
              placeholder="Tìm đơn/KH…"
              className="input-field"
            />
          </div>
        </header>

        {/* Page content */}
        <main className="p-4 md:p-6 max-w-7xl mx-auto">
          {children}
        </main>
      </div>
    </div>
  );
}
