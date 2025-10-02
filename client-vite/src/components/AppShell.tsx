import { Link, NavLink, Outlet } from 'react-router-dom';
import { paths } from '@/routes/paths';

const linkCls = ({ isActive }: { isActive: boolean }) =>
  `px-3 py-2 rounded-md text-sm font-medium ${isActive ? 'bg-gray-900 text-white' : 'text-gray-300 hover:bg-gray-700 hover:text-white'}`;

export default function AppShell() {
  return (
    <div className="min-h-screen bg-gray-100">
      <header className="bg-gray-800">
        <div className="mx-auto max-w-7xl px-4 sm:px-6 lg:px-8">
          <div className="flex h-16 items-center justify-between">
            <Link to={paths.cards} className="text-white font-semibold">TCG Tracker</Link>
            <nav className="flex gap-1">
              <NavLink to={paths.cards} className={linkCls}>Cards</NavLink>
              <NavLink to={paths.collection} className={linkCls}>Collection</NavLink>
              <NavLink to={paths.wishlist} className={linkCls}>Wishlist</NavLink>
              <NavLink to={paths.decks} className={linkCls}>Decks</NavLink>
              <NavLink to={paths.value} className={linkCls}>Value</NavLink>
              <NavLink to={paths.users} className={linkCls}>Users</NavLink>
              <NavLink to={paths.adminImport} className={linkCls}>Admin · Import</NavLink>
            </nav>
          </div>
        </div>
      </header>
      <main className="mx-auto max-w-7xl p-4">
        <Outlet />
      </main>
    </div>
  );
}
