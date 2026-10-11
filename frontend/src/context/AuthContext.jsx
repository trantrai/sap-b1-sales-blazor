import { createContext, useContext, useState, useEffect } from 'react';
import api from '../api/client';

const AuthContext = createContext(null);

export function AuthProvider({ children }) {
  const [user, setUser] = useState(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    const token = localStorage.getItem('access_token');
    if (token) {
      try {
        const payload = JSON.parse(atob(token.split('.')[1]));
        setUser({ username: payload.sub, role: payload.role, companyDb: payload.b1_db });
      } catch {
        localStorage.clear();
      }
    }
    setLoading(false);
  }, []);

  const login = async (username, password, companyDb) => {
    const { data } = await api.post('/auth/login', {
      username, password, company_db: companyDb,
    });
    localStorage.setItem('access_token', data.access_token);
    localStorage.setItem('refresh_token', data.refresh_token);
    const payload = JSON.parse(atob(data.access_token.split('.')[1]));
    setUser({ username: payload.sub, role: payload.role, companyDb: payload.b1_db });
  };

  const logout = async () => {
    try { await api.post('/auth/logout'); } catch {}
    localStorage.clear();
    setUser(null);
  };

  return (
    <AuthContext.Provider value={{ user, loading, login, logout }}>
      {children}
    </AuthContext.Provider>
  );
}

export const useAuth = () => useContext(AuthContext);
