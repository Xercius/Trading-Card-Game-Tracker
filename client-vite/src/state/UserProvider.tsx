import { useEffect, useMemo, useState, useCallback } from "react";
import { useQueryClient } from "@tanstack/react-query";
import http, { setHttpUserId } from "@/lib/http";
import { mapUser } from "@/lib/mapUser";
import type { AdminUserApi, ApiUser, UserLite } from "@/types/user";
import { UserContext } from "./UserContext";

export function UserProvider({ children }: { children: React.ReactNode }) {
  const qc = useQueryClient();
  const [userId, setUserIdState] = useState<number | null>(null);
  const [users, setUsers] = useState<UserLite[]>([]);

  const applyUserId = useCallback((id: number | null) => {
    setUserIdState(id);
    setHttpUserId(id);
    if (typeof window !== "undefined") {
      const storage = window.localStorage;
      if (id != null) storage.setItem("userId", String(id));
      else storage.removeItem("userId");
    }
  }, []);

  const refreshUsers = useCallback(async () => {
    try {
      const meRes = await http.get<ApiUser>("user/me");
      const meLite = mapUser(meRes.data);
      applyUserId(meLite.id);

      if (!meLite.isAdmin) {
        setUsers([meLite]);
        return;
      }

      const listRes = await http.get<AdminUserApi[]>("admin/users");
      setUsers(listRes.data.map(mapUser));
    } catch (err) {
      // eslint-disable-next-line no-console
      console.error("[UserContext] Failed to refresh users:", err);
      setUsers([]);
      applyUserId(null);
    }
  }, [applyUserId]);

  useEffect(() => {
    const saved = typeof window !== "undefined" ? window.localStorage.getItem("userId") : null;
    const parsed = saved ? Number(saved) : NaN;
    const initial = Number.isFinite(parsed) && parsed > 0 ? parsed : 1;
    applyUserId(initial);
    void refreshUsers();
  }, [applyUserId, refreshUsers]);

  const setUserId = useCallback(
    (id: number) => {
      applyUserId(id);
      qc.invalidateQueries();
    },
    [applyUserId, qc]
  );

  const value = useMemo(
    () => ({ userId, setUserId, users, refreshUsers }),
    [userId, users, setUserId, refreshUsers]
  );

  return <UserContext.Provider value={value}>{children}</UserContext.Provider>;
}
