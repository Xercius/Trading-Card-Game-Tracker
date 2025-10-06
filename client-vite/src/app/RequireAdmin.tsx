import type { ReactElement } from "react";
import { Navigate } from "react-router-dom";
import { useUser } from "@/state/useUser";
import { paths } from "@/routes/paths";

export function RequireAdmin({ children }: { children: ReactElement }) {
  const { users, userId } = useUser();
  const me = users.find((u) => u.id === userId);
  if (!me?.isAdmin) return <Navigate to={paths.cards} replace />;
  return children;
}
