import { createContext, useCallback, useContext, useEffect, useMemo, useState } from "react";
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

  useEffect(() => {
    const saved = localStorage.getItem("userId");
    const parsed = saved ? Number(saved) : NaN;
    const initial = Number.isFinite(parsed) ? parsed : null;
    setUserIdState(initial);
    setHttpUserId(initial);
  }, []);

  const refreshUsers = useCallback(async () => {
    try {
      const meRes = await http.get<ApiUser>("user/me");
      const me = mapUser(meRes.data);

      if (userId == null) {
        setUserIdState(me.id);
        localStorage.setItem("userId", String(me.id));
        setHttpUserId(me.id);
      }

      if (me.isAdmin) {
        const listRes = await http.get<ApiUser[]>("user");
        setUsers(listRes.data.map(mapUser));
      } else {
        setUsers([me]);
      }
    } catch (err) {
      setUsers([]);
      if (import.meta.env.DEV) {
        // eslint-disable-next-line no-console
        console.warn("[UserContext] Failed to load user session", err);
      }
    }
  }, [userId]);

  useEffect(() => {
    void refreshUsers();
  }, [refreshUsers]);

  const setUserId = (id: number) => {
    setUserIdState(id);
    localStorage.setItem("userId", String(id));
    setHttpUserId(id);
    qc.invalidateQueries();
  };

  const value = useMemo(
    () => ({ userId, setUserId, users, refreshUsers }),
    [userId, users, refreshUsers]
  );
  return <UserContext.Provider value={value}>{children}</UserContext.Provider>;
}

export function useUser() {
  const ctx = useContext(UserContext);
  if (!ctx) throw new Error("useUser must be inside UserProvider");
  return ctx;
}
