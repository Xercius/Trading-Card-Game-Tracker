import { createContext } from "react";
import type { UserLite } from "@/types/user";

export type Ctx = {
  userId: number | null;
  setUserId: (id: number) => void;
  users: UserLite[];
  refreshUsers: () => Promise<void>;
};

export const UserContext = createContext<Ctx | null>(null);
