import { createContext } from "react";
import type { UserLite } from "@/types/user";

export const OWNER_USER_ID = 1;
export const OWNER_USER: UserLite = { id: OWNER_USER_ID, name: "Owner", isAdmin: false };

export type Ctx = {
  userId: number;
  users: UserLite[];
};

export const UserContext = createContext<Ctx | null>(null);
