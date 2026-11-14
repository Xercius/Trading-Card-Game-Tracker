import { Suspense, lazy } from "react";
import { Navigate, RouterProvider, createBrowserRouter } from "react-router-dom";
import AppShell from "@/app/AppShell";
import { UserProvider } from "@/state/UserProvider";
import { GameProvider } from "@/state/GameProvider";
import { paths } from "@/routes/paths";
import { RequireAdmin } from "@/app/RequireAdmin";
import { ErrorBoundary } from "@/components/ErrorBoundary";

const CardsPage = lazy(() => import("@/pages/CardsPage"));
const CollectionPage = lazy(() => import("@/pages/CollectionPage"));
const CollectionSetDetailPage = lazy(() => import("@/pages/CollectionSetDetailPage"));
const WishlistPage = lazy(() => import("@/pages/WishlistPage"));
const DecksPage = lazy(() => import("@/pages/DecksPage"));
const DeckBuilderPage = lazy(() => import("@/pages/DeckBuilderPage"));
const AdminSyncPanel = lazy(() => import("@/features/admin/sync/AdminSyncPanel"));
const AdminSwccgSyncPanel = lazy(() => import("@/features/admin/sync/AdminSwccgSyncPanel"));
const AdminPokemonSyncPanel = lazy(() => import("@/features/admin/sync/AdminPokemonSyncPanel"));
const UsersPage = lazy(() => import("@/pages/UsersPage"));
const ValueHubPage = lazy(() => import("@/pages/ValueHubPage"));
const NotFoundPage = lazy(() => import("@/pages/NotFoundPage"));

function withBoundary(element: React.ReactNode) {
  return <ErrorBoundary>{element}</ErrorBoundary>;
}

const router = createBrowserRouter([
  {
    path: "/",
    element: (
      <ErrorBoundary>
        <UserProvider>
          <GameProvider>
            <AppShell />
          </GameProvider>
        </UserProvider>
      </ErrorBoundary>
    ),
    children: [
      { index: true, element: <Navigate to={paths.cards} replace /> },
      { path: paths.cards, element: withBoundary(<CardsPage />) },
      { path: paths.collection, element: withBoundary(<CollectionPage />) },
      { path: paths.collectionSetDetail, element: withBoundary(<CollectionSetDetailPage />) },
      { path: paths.wishlist, element: withBoundary(<WishlistPage />) },
      { path: `${paths.decks}/:deckId`, element: withBoundary(<DeckBuilderPage />) },
      { path: paths.decks, element: withBoundary(<DecksPage />) },
      { path: paths.value, element: withBoundary(<ValueHubPage />) },
      {
        path: paths.users,
        element: withBoundary(
          <RequireAdmin>
            <UsersPage />
          </RequireAdmin>
        ),
      },
      {
        path: paths.adminSync,
        element: withBoundary(<AdminSyncPanel />),
      },
      {
        path: paths.adminSwccgSync,
        element: withBoundary(<AdminSwccgSyncPanel />),
      },
      {
        path: paths.adminPokemonSync,
        element: withBoundary(<AdminPokemonSyncPanel />),
      },
      { path: "*", element: withBoundary(<NotFoundPage />) },
    ],
  },
]);

/** Root application router with Suspense and ErrorBoundary protection. */
export default function AppRouter() {
  return (
    <Suspense fallback={<div className="p-4">Loading…</div>}>
      <RouterProvider router={router} />
    </Suspense>
  );
}
