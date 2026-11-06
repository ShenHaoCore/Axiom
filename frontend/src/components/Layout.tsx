// 页面外壳：导航、当前用户与退出登录。
import { NavLink, Outlet } from 'react-router-dom';
import { logout } from '../api/news';
import type { CurrentUser } from '../types';

export default function Layout({ user }: { user: CurrentUser }) {
  async function handleLogout() {
    try {
      await logout();
    } finally {
      window.location.href = '/app/';
    }
  }

  return (
    <div className="page">
      <header className="app-header">
        <nav>
          <NavLink to="/" end>
            新闻列表
          </NavLink>
          <NavLink to="/news/new">新建新闻</NavLink>
        </nav>
        <div className="user-box">
          <span>{user.name}</span>
          <button type="button" className="secondary" onClick={handleLogout}>
            退出
          </button>
        </div>
      </header>
      <main>
        <Outlet />
      </main>
    </div>
  );
}
