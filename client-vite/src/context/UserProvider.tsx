// client-vite/src/context/UserProvider.tsx
import { useCallback, useEffect, useMemo, useState } from "react";
import { useQueryClient } from "@tanstack/react-query";
import axios from "axios";
import { UserCtx } from "./UserCtx"; // existing context object

export function UserProvider({ children }: { children: React.ReactNode }) {
  const qc = useQueryClient();
  const isBrowser = typeof window !== "undefined" && typeof localStorage !== "undefined";

  const [apiUserId, setApiUserId] = useState<number>(() => {
    if (!isBrowser) return 1;
    const raw = localStorage.getItem("apiUserId");
    return raw ? Number(raw) || 1 : 1;
  });

  const setUserId = useCallback(
    (id: number) => {
      if (isBrowser) {
        localStorage.setItem("apiUserId", String(id));
      }
      setApiUserId(id);
      qc.invalidateQueries();
    },
    [qc, isBrowser]
  );

  useEffect(() => {
    if (!isBrowser) return;
    localStorage.setItem("apiUserId", String(apiUserId));
    axios.defaults.headers.common["X-User-Id"] = String(apiUserId);
  }, [apiUserId, isBrowser]);

  const value = useMemo(() => ({ apiUserId, setApiUserId: setUserId }), [apiUserId, setUserId]);

  return <UserCtx.Provider value={value}>{children}</UserCtx.Provider>;
}
