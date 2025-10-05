import { useEffect, useMemo, useState, useCallback } from "react";
import { useQueryClient } from "@tanstack/react-query";
import http, { setHttpUserId } from "@/lib/http";
import { mapUser } from "@/lib/mapUser";
import type { ApiUser, UserLite } from "@/types/user";
import { UserContext } from "./UserContext";

export function UserProvider({ children }: { children: React.ReactNode }) {
  const qc = useQueryClient();
  const [userId, setUserIdState] = useState<number | null>(null);
  const [users, setUsers] = useState<UserLite[]>([]);

  useEffect(() => {
    const saved = localStorage.getItem("userId");
    let initial = 1;
    if (saved) {
      const parsed = Number(saved);
      if (Number.isFinite(parsed) && parsed > 0) initial = parsed;
    }
    setUserIdState(initial);
    localStorage.setItem("userId", String(initial));
    setHttpUserId(initial);
  }, []);

  const refreshUsers = useCallback(async () => {
    try {
      const meRes = await http.get<ApiUser>("user/me");
      const meLite = mapUser(meRes.data);

      if (userId !== meLite.id) {
        setUserIdState(meLite.id);
        localStorage.setItem("userId", String(meLite.id));
        setHttpUserId(meLite.id);
      }

      if (!meLite.isAdmin) {
        setUsers([meLite]);
        return;
      }

      const listRes = await http.get<ApiUser[]>("user");
      setUsers(listRes.data.map(mapUser));
    } catch (err) {
      console.error("[UserContext] Failed to refresh users:", err);
      setUsers([]);
    }
  }, [userId]);

  useEffect(() => {
    void refreshUsers();
  }, [refreshUsers]);

  const setUserId = useCallback(
    (id: number) => {
      setUserIdState(id);
      localStorage.setItem("userId", String(id));
      setHttpUserId(id);
      qc.invalidateQueries();
    },
    [qc]
  );

  const value = useMemo(
    () => ({ userId, setUserId, users, refreshUsers }),
    [userId, users, setUserId, refreshUsers]
  );

  return <UserContext.Provider value={value}>{children}</UserContext.Provider>;
}
