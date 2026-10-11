// components/Layout.tsx — Sidebar + topbar theo thiết kế (Next.js App Router).
'use client';

import Link from 'next/link';
import { usePathname, useRouter } from 'next/navigation';
import { useAuth } from '@/context/AuthContext';
import { useState, ReactNode, Suspense } from 'react';

const menuItems = [
  { to: '/dashboard', label: 'Dashboard', icon: '📊' },
  { to: '/', label: 'Danh sách đơn', icon: '📋' },
  { to: '/orders/new', label: 'Tạo đơn hàng', icon: '➕' },
  { to: '/sync', label: 'Đồng bộ dữ liệu', icon: '🔄' },
  { to: '/monitor', label: 'Giám sát tích hợp', icon: '👁️', badge: '2' },
  { to: '/audit', label: 'Audit người dùng', icon: '📝' },
  { to: '/users', label: 'Người dùng & phân quyền', icon: '👥' },
  { to: '/settings', label: 'Cài đặt kết nối', icon: '⚙️' },
];

function SidebarContent({ onNavigate }: { onNavigate?: () => void }) {
  const { user, logout } = useAuth();
  const router = useRouter();
  const pathname = usePathname();

  const handleLogout = async () => {
    await logout();
    router.push('/login');
  };

  return (
    <>
      <div className="p-4">
        <div className="flex items-center gap-3">
          <div
            className="w-10 h-10 rounded-lg flex items-center justify-center text-sm font-bold"
            style={{ background: 'var(--yellow)', color: 'var(--navy-900)' }}
          >
            SO
          </div>
          <div>
            <div className="font-bold text-sm" style={{ color: '#fff' }}>
              SALE ORDER
            </div>
            <div className="text-xs" style={{ color: '#d6e2f5' }}>
              SAP B1 - HANA
            </div>
          </div>
        </div>
      </div>
      <nav className="flex-1 px-3 py-2 space-y-1 overflow-y-auto">
        {menuItems.map((item) => {
          const isActive =
            item.to === '/' ? pathname === '/' : pathname.startsWith(item.to);
          return (
            <Link
              key={item.to}
              href={item.to}
              onClick={onNavigate}
              className={`flex items-center gap-3 px-3.5 py-2.5 rounded-xl text-sm transition-colors relative ${
                isActive ? 'font-bold' : ''
              }`}
              style={
                isActive
                  ? { background: 'var(--yellow)', color: 'var(--navy-900)' }
                  : { color: '#d6e2f5' }
              }
            >
              <span className="text-base">{item.icon}</span>
              <span className="flex-1">{item.label}</span>
              {item.badge && (
                <span
                  className="text-white text-xs min-w-[20px] h-5 px-1.5 rounded-full flex items-center justify-center font-bold"
                  style={{ background: 'var(--orange)' }}
                >
                  {item.badge}
                </span>
              )}
            </Link>
          );
        })}
      </nav>
      <div className="p-4" style={{ color: '#d6e2f5' }}>
        <div className="text-sm mb-1 font-medium" style={{ color: '#fff' }}>
          {user?.username === 'admin' ? 'Administrator' : user?.username}
        </div>
        <div className="mb-3">
          <span
            className="text-xs px-2 py-0.5 rounded-full font-medium"
            style={{ background: 'var(--yellow)', color: 'var(--navy-900)' }}
          >
            {user?.role}
          </span>
        </div>
        <button
          onClick={handleLogout}
          className="flex items-center gap-2 text-sm hover:opacity-80"
          style={{ color: '#d6e2f5' }}
        >
          <span>🚪</span> Đăng xuất
        </button>
      </div>
    </>
  );
}

export default function Layout({ children, title }: { children: ReactNode; title: string }) {
  const [sidebarOpen, setSidebarOpen] = useState(false);
  const [dark, setDark] = useState(false);
  const [globalSearch, setGlobalSearch] = useState('');

  return (
    <div className={`min-h-screen ${dark ? 'dark' : ''}`}>
      {/* Sidebar - Desktop */}
      <aside
        className="hidden md:flex fixed inset-y-0 left-0 w-60 flex-col z-40"
        style={{ background: 'var(--navy-900)' }}
      >
        <Suspense>
          <SidebarContent />
        </Suspense>
      </aside>

      {/* Sidebar - Mobile drawer */}
      {sidebarOpen && (
        <>
          <div
            className="fixed inset-0 bg-black/50 z-40 md:hidden"
            onClick={() => setSidebarOpen(false)}
          />
          <aside
            className="fixed inset-y-0 left-0 w-64 flex flex-col z-50 md:hidden"
            style={{ background: 'var(--navy-900)' }}
          >
            <Suspense>
              <SidebarContent onNavigate={() => setSidebarOpen(false)} />
            </Suspense>
          </aside>
        </>
      )}

      {/* Main content */}
      <div className="md:ml-60">
        {/* Topbar */}
        <header className="sticky top-0 z-30 bg-white border-b-2 border-brand-yellow">
          <div className="flex items-center gap-3 px-4 py-2.5">
            <button
              onClick={() => setSidebarOpen(true)}
              className="md:hidden p-2 rounded-lg hover:bg-slate-100 text-xl"
              aria-label="Mở menu"
            >
              ☰
            </button>
            <h2 className="text-base font-semibold text-navy-900 flex-1">{title}</h2>
            <div className="hidden md:flex items-center gap-2">
              <div className="relative">
                <input
                  type="search"
                  value={globalSearch}
                  onChange={(e) => setGlobalSearch(e.target.value)}
                  placeholder="Tìm đơn/KH... (Ctrl+K)"
                  className="w-56 px-3 py-1.5 text-sm border border-slate-200 rounded-lg bg-slate-50 focus:outline-none focus:border-brand-blue"
                />
              </div>
              <button
                onClick={() => setDark(!dark)}
                className="p-2 rounded-lg hover:bg-slate-100"
                aria-label="Chế độ tối/sáng"
              >
                {dark ? '☀️' : '🌙'}
              </button>
              <span className="inline-flex items-center gap-1.5 px-2.5 py-1 rounded-full text-xs font-medium bg-green-100 text-green-700">
                <span className="w-2 h-2 bg-green-500 rounded-full"></span>
                B1 mock
              </span>
            </div>
            <button
              onClick={() => setDark(!dark)}
              className="md:hidden p-2 rounded-lg hover:bg-slate-100"
            >
              {dark ? '☀️' : '🌙'}
            </button>
          </div>
          <div className="px-4 pb-2.5 md:hidden">
            <input
              type="search"
              placeholder="Tìm đơn/KH..."
              className="w-full px-3 py-2 text-sm border border-slate-200 rounded-lg bg-slate-50"
            />
          </div>
        </header>

        {/* Page content */}
        <main className="p-4 md:p-6 bg-[#faf8f1] min-h-[calc(100vh-60px)]">{children}</main>
      </div>
    </div>
  );
}
