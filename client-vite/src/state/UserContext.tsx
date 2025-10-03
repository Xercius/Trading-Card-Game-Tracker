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
    // 1) Try full list (works for admins or if API allows everyone)
    const listRes = await http.get<ApiUser[]>("user");
    const list = (listRes.data ?? []).map(mapUser);

    if (list.length) {
      setUsers(list);
      if (userId == null) {
        const first = list[0].id;
        setUserIdState(first);
        localStorage.setItem("userId", String(first));
        setHttpUserId(first);
      }
      return;
    }
  } catch {
    // ignore and try /user/me next
  }

  try {
    // 2) Fallback: current user only
    const meRes = await http.get<ApiUser>("user/me");
    const me = mapUser(meRes.data);
    setUsers([me]);

    if (userId == null) {
      setUserIdState(me.id);
      localStorage.setItem("userId", String(me.id));
      setHttpUserId(me.id);
    }
    return;
  } catch {
    // ignore and fallback below
  }

  // 3) Last-resort fallback so the UI is usable
  const fallback: UserLite[] = [{ id: 1, name: "Me", isAdmin: false }];
  setUsers(fallback);

  if (userId == null) {
    setUserIdState(1);
    localStorage.setItem("userId", "1");
    setHttpUserId(1);
  }

  if (import.meta.env.DEV) {
    // eslint-disable-next-line no-console
    console.warn("[UserContext] Could not load users; using fallback");
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
