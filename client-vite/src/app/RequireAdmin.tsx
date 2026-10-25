import type { ReactElement } from "react";
import { Navigate } from "react-router-dom";
import { useUser } from "@/state/useUser";
import { paths } from "@/routes/paths";
import { OWNER_USER_ID } from "@/state/UserContext";

export function RequireAdmin({
  children,
  allowOwner = false,
}: {
  children: ReactElement;
  allowOwner?: boolean;
}) {
  const { users, userId } = useUser();
  const me = users.find((u) => u.id === userId);
  if (!me?.isAdmin && !(allowOwner && userId === OWNER_USER_ID)) {
    return <Navigate to={`/${paths.cards}`} replace />;
  }
  return children;
}
