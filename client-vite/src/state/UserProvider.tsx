import type { ReactNode } from "react";
import { OWNER_USER, OWNER_USER_ID, UserContext, type Ctx } from "./UserContext";

const ownerUsers = [OWNER_USER];
const ownerContext: Ctx = { userId: OWNER_USER_ID, users: ownerUsers };

export function UserProvider({ children }: { children: ReactNode }) {
  return <UserContext.Provider value={ownerContext}>{children}</UserContext.Provider>;
}
