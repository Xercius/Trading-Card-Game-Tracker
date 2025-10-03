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

  /**
   * Manual QA (DEV only):
   * 1) Open DevTools → Network.
   * 2) Change the user from the header dropdown.
   * 3) Observe a new request (e.g., GET user/me) fired by this effect.
   * 4) Click the request → Headers tab → Request Headers.
   * 5) Confirm `X-User-Id: <selected id>` matches the new selection.
   * 6) Navigate around (Cards/Collection/etc.) and confirm subsequent requests also carry the new header.
   *
   * NOTE: This effect and debug UI are DEV-only and safe to keep;
   * remove them later if you prefer a cleaner console.
   */
  useEffect(() => {
    if (!import.meta.env.DEV || userId == null) return;

    (async () => {
      const endpoints = ["user/me", "card"];
      let lastError: unknown = null;

      for (const endpoint of endpoints) {
        try {
          const res = await http.get(endpoint);
          console.info("[X-User-Id verify]", { userId, status: res.status, endpoint });
          return;
        } catch (error) {
          lastError = error;
        }
      }

      console.info("[X-User-Id verify] request failed", {
        userId,
        error: lastError,
        endpoints,
      });
    })();
  }, [userId]);

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
