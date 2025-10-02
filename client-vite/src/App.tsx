import { Suspense, lazy } from 'react';
import { Routes, Route, Navigate } from 'react-router-dom';
import AppShell from '@/components/AppShell';
import { paths } from '@/routes/paths';
import CardsPage from '@/pages/CardsPage';

const CollectionPage  = lazy(() => import('@/pages/CollectionPage'));
const WishlistPage    = lazy(() => import('@/pages/WishlistPage'));
const DecksPage       = lazy(() => import('@/pages/DecksPage'));
const AdminImportPage = lazy(() => import('@/pages/AdminImportPage'));
const UsersPage       = lazy(() => import('@/pages/UsersPage'));
const ValueHubPage    = lazy(() => import('@/pages/ValueHubPage'));
const NotFoundPage    = lazy(() => import('@/pages/NotFoundPage'));

function Loader() { return <div className="p-4">Loading…</div>; }

export default function App() {
  return (
    <Suspense fallback={<Loader />}>
      <Routes>
        <Route element={<AppShell />}>
          <Route index element={<Navigate to={paths.cards} replace />} />
          <Route path={paths.cards} element={<CardsPage />} />
          <Route path={paths.collection} element={<CollectionPage />} />
          <Route path={paths.wishlist} element={<WishlistPage />} />
          <Route path={paths.decks} element={<DecksPage />} />
          <Route path={paths.value} element={<ValueHubPage />} />
          <Route path={paths.users} element={<UsersPage />} />
          <Route path={paths.adminImport} element={<AdminImportPage />} />
          <Route path="*" element={<NotFoundPage />} />
        </Route>
      </Routes>
    </Suspense>
  );
}