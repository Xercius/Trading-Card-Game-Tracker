import { Suspense, lazy } from "react";
import { Navigate, RouterProvider, createBrowserRouter } from "react-router-dom";
import AppShell from "@/app/AppShell";
import { UserProvider } from "@/state/UserProvider";
import { GameProvider } from "@/state/GameProvider";
import CardsPage from "@/features/printings/pages/CardsPage";
import { paths } from "@/routes/paths";
import { RequireAdmin } from "@/app/RequireAdmin";

const CollectionPage = lazy(() => import("@/pages/CollectionPage"));
const CollectionSetDetailPage = lazy(() => import("@/pages/CollectionSetDetailPage"));
const WishlistPage = lazy(() => import("@/pages/WishlistPage"));
const DecksPage = lazy(() => import("@/pages/DecksPage"));
const DeckBuilderPage = lazy(() => import("@/pages/DeckBuilderPage"));
const AdminImportPage = lazy(() => import("@/pages/AdminImportPage"));
const AdminSyncPanel = lazy(() => import("@/features/admin/sync/AdminSyncPanel"));
const AdminSwccgSyncPanel = lazy(() => import("@/features/admin/sync/AdminSwccgSyncPanel"));
const AdminPokemonSyncPanel = lazy(() => import("@/features/admin/sync/AdminPokemonSyncPanel"));
const UsersPage = lazy(() => import("@/pages/UsersPage"));
const ValueHubPage = lazy(() => import("@/pages/ValueHubPage"));
const NotFoundPage = lazy(() => import("@/pages/NotFoundPage"));

const router = createBrowserRouter([
  {
    path: "/",
    element: (
      <UserProvider>
        <GameProvider>
          <AppShell />
        </GameProvider>
      </UserProvider>
    ),
    children: [
      { index: true, element: <Navigate to={paths.cards} replace /> },
      { path: paths.cards, element: <CardsPage /> },
      { path: paths.collection, element: <CollectionPage /> },
      { path: paths.collectionSetDetail, element: <CollectionSetDetailPage /> },
      { path: paths.wishlist, element: <WishlistPage /> },
      { path: `${paths.decks}/:deckId`, element: <DeckBuilderPage /> },
      { path: paths.decks, element: <DecksPage /> },
      { path: paths.value, element: <ValueHubPage /> },
      {
        path: paths.users,
        element: (
          <RequireAdmin>
            <UsersPage />
          </RequireAdmin>
        ),
      },
      {
        path: paths.adminImport,
        element: (
          <RequireAdmin allowOwner>
            <AdminImportPage />
          </RequireAdmin>
        ),
      },
      { path: paths.adminSync, element: <AdminSyncPanel /> },
      { path: paths.adminSwccgSync, element: <AdminSwccgSyncPanel /> },
      { path: paths.adminPokemonSync, element: <AdminPokemonSyncPanel /> },
      { path: "*", element: <NotFoundPage /> },
    ],
  },
]);

export default function AppRouter() {
  return (
    <Suspense fallback={<div className="p-4">Loading…</div>}>
      <RouterProvider router={router} />
    </Suspense>
  );
}
