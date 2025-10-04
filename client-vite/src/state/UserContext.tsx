// client-vite/src/state/UserContext.tsx
import { createContext, useContext, useEffect, useMemo, useState } from "react";
import { useQueryClient } from "@tanstack/react-query";
import http, { setHttpUserId } from "@/lib/http";
import { mapUser } from "@/lib/mapUser";
import type { ApiUser, UserLite } from "@/types/user";

type Ctx = {
  userId: number | null;
  setUserId: (id: number) => void;
  users: UserLite[];
  refreshUsers: () => Promise<void>;
};

const UserContext = createContext<Ctx | null>(null);

export function UserProvider({ children }: { children: React.ReactNode }) {
  const qc = useQueryClient();
  const [userId, setUserIdState] = useState<number | null>(null);
  const [users, setUsers] = useState<UserLite[]>([]);

  // Initialize header from persisted selection if present
  useEffect(() => {
  const saved = localStorage.getItem("userId");
  let initial: number = 1;                       // default to 1
  if (saved) {
    const parsed = Number(saved);
    if (Number.isFinite(parsed) && parsed > 0) initial = parsed;
  }
  setUserIdState(initial);
  localStorage.setItem("userId", String(initial)); // persist the default
  setHttpUserId(initial);                          // stamp header immediately
}, []);


  async function refreshUsers() {
    try {
      // Always resolve the current caller first
      const meRes = await http.get<ApiUser>("user/me");
      const meLite = mapUser(meRes.data);

      // Ensure we have a selected user id matching "me"
      if (userId !== meLite.id) {
        setUserIdState(meLite.id);
        localStorage.setItem("userId", String(meLite.id));
        setHttpUserId(meLite.id);
      }

      // Non-admins get just themselves; admins get the full list
      if (!meLite.isAdmin) {
        setUsers([meLite]);
        return;
      }

      const listRes = await http.get<ApiUser[]>("user");
      const list: UserLite[] = listRes.data.map(mapUser);
      setUsers(list);
    } catch (err) {
      // Do NOT force-set a default user id (e.g., 1). Surface the empty state instead.
      // eslint-disable-next-line no-console
      console.error("[UserContext] Failed to refresh users:", err);
      setUsers([]);
    }
  }

  useEffect(() => {
    void refreshUsers();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  const setUserId = (id: number) => {
    setUserIdState(id);
    localStorage.setItem("userId", String(id));
    setHttpUserId(id);
    qc.invalidateQueries();
  };

  const value = useMemo(
    () => ({ userId, setUserId, users, refreshUsers }),
    [userId, users]
  );

  return <UserContext.Provider value={value}>{children}</UserContext.Provider>;
}

export function useUser() {
  const ctx = useContext(UserContext);
  if (!ctx) throw new Error("useUser must be inside UserProvider");
  return ctx;
}
