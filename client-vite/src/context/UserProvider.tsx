// client-vite/src/providers/UserProvider.tsx
import { createContext, useEffect, useMemo, useState } from "react";
import axios from "axios";

type UserCtx = {
  apiUserId: number;
  setApiUserId: (id: number) => void;
};

export const UserContext = createContext<UserCtx>({
  apiUserId: 1,
  setApiUserId: () => {},
});

export function UserProvider({ children }: { children: React.ReactNode }) {
  const isBrowser = typeof window !== "undefined" && typeof localStorage !== "undefined";

  // Safe initializer: returns 1 when window/localStorage not available
  const [apiUserId, setApiUserId] = useState<number>(() => {
    if (!isBrowser) return 1;
    const raw = localStorage.getItem("apiUserId");
    return raw ? Number(raw) || 1 : 1;
  });

  // Persist changes only when browser is available
  useEffect(() => {
    if (!isBrowser) return;
    localStorage.setItem("apiUserId", String(apiUserId));
    // update axios header for API calls
    axios.defaults.headers.common["X-User-Id"] = String(apiUserId);
  }, [apiUserId, isBrowser]);

  const value = useMemo(() => ({ apiUserId, setApiUserId }), [apiUserId]);

  return <UserContext.Provider value={value}>{children}</UserContext.Provider>;
}
