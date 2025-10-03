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

  useEffect(() => {
    const saved = localStorage.getItem("userId");
    let initial: number | null = null;
    if (saved) {
      const parsed = Number(saved);
      initial = Number.isFinite(parsed) ? parsed : null;
    }
    setUserIdState(initial);
    setHttpUserId(initial);
  }, []);

  async function refreshUsers() {
    try {
      const res = await http.get<ApiUser[]>("user");
      const list: UserLite[] = res.data.map(mapUser);
      setUsers(list);
      if (!userId && list.length) {
        const first = list[0].id;
        setUserIdState(first);
        localStorage.setItem("userId", String(first));
        setHttpUserId(first);
      }
    } catch {
      const fallback: UserLite[] = [{ id: 1, name: "Me", isAdmin: false }];
      setUsers(fallback);
      if (!userId) {
        setUserIdState(1);
        localStorage.setItem("userId", "1");
        setHttpUserId(1);
      }
    }
  }

  useEffect(() => {
    void refreshUsers();
  }, []);

  const setUserId = (id: number) => {
    setUserIdState(id);
    localStorage.setItem("userId", String(id));
    setHttpUserId(id);
    qc.invalidateQueries();
  };

  const value = useMemo(() => ({ userId, setUserId, users, refreshUsers }), [userId, users]);
  return <UserContext.Provider value={value}>{children}</UserContext.Provider>;
}

export function useUser() {
  const ctx = useContext(UserContext);
  if (!ctx) throw new Error("useUser must be inside UserProvider");
  return ctx;
}
