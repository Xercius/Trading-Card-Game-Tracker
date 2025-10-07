import { Suspense, lazy } from "react";
import { Navigate, RouterProvider, createBrowserRouter } from "react-router-dom";
import AppShell from "@/app/AppShell";
import { UserProvider } from "@/state/UserProvider";
import CardsPage from "@/pages/CardsPage";
import { paths } from "@/routes/paths";
import { RequireAdmin } from "@/app/RequireAdmin";

const CollectionPage  = lazy(() => import("@/pages/CollectionPage"));
const WishlistPage    = lazy(() => import("@/pages/WishlistPage"));
const DecksPage       = lazy(() => import("@/pages/DecksPage"));
const DeckBuilderPage = lazy(() => import("@/pages/DeckBuilderPage"));
const AdminImportPage = lazy(() => import("@/pages/AdminImportPage"));
const UsersPage       = lazy(() => import("@/pages/UsersPage"));
const ValueHubPage    = lazy(() => import("@/pages/ValueHubPage"));
const NotFoundPage    = lazy(() => import("@/pages/NotFoundPage"));

const router = createBrowserRouter([
  {
    path: "/",
    element: <AppShell />,
    children: [
      { index: true, element: <Navigate to={paths.cards} replace /> },
      { path: paths.cards, element: <CardsPage /> },
      { path: paths.collection, element: <CollectionPage /> },
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
          <RequireAdmin>
            <AdminImportPage />
          </RequireAdmin>
        ),
      },
      { path: "*", element: <NotFoundPage /> },
    ],
  },
]);

export default function AppRouter() {
  return (
    <UserProvider>
      <Suspense fallback={<div className="p-4">Loading…</div>}>
        <RouterProvider router={router} />
      </Suspense>
    </UserProvider>
  );
}
