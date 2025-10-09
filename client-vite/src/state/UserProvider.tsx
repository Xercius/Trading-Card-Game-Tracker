import { useEffect, useMemo, useState, useCallback } from "react";
import { useQueryClient } from "@tanstack/react-query";
import http, { setHttpAccessToken } from "@/lib/http";
import { mapUser } from "@/lib/mapUser";
import type { AdminUserApi, ApiUser, UserLite } from "@/types/user";
import { UserContext } from "./UserContext";

type LoginResponse = {
  accessToken: string;
  expiresAtUtc: string;
  user: ApiUser;
};

export function UserProvider({ children }: { children: React.ReactNode }) {
  const qc = useQueryClient();
  const [accessToken, setAccessToken] = useState<string | null>(null);
  const [userId, setUserIdState] = useState<number | null>(null);
  const [users, setUsers] = useState<UserLite[]>([]);
  const [pickerUsers, setPickerUsers] = useState<UserLite[]>([]);
  const [pickerError, setPickerError] = useState<string | null>(null);
  const [pickerLoading, setPickerLoading] = useState(false);

  const persistToken = useCallback((token: string | null) => {
    setAccessToken(token);
    setHttpAccessToken(token);
    if (typeof window !== "undefined") {
      const storage = window.localStorage;
      if (token) storage.setItem("authToken", token);
      else storage.removeItem("authToken");
    }
  }, []);

  const clearUserState = useCallback(() => {
    setUserIdState(null);
    setUsers([]);
  }, []);

  const populateUsers = useCallback(async () => {
    const meRes = await http.get<ApiUser>("user/me");
    const meLite = mapUser(meRes.data);
    setUserIdState(meLite.id);

    if (!meLite.isAdmin) {
      setUsers([meLite]);
      return;
    }

    const listRes = await http.get<AdminUserApi[]>("admin/users");
    setUsers(listRes.data.map(mapUser));
  }, []);

  const refreshUsers = useCallback(async () => {
    if (!accessToken) return;
    try {
      await populateUsers();
    } catch (err) {
      // eslint-disable-next-line no-console
      console.error("[UserContext] Failed to refresh users:", err);
      persistToken(null);
      clearUserState();
    }
  }, [accessToken, populateUsers, persistToken, clearUserState]);

  useEffect(() => {
    const saved = typeof window !== "undefined" ? window.localStorage.getItem("authToken") : null;
    if (saved) {
      persistToken(saved);
      populateUsers().catch((error) => {
        // eslint-disable-next-line no-console
        console.error("[UserContext] Failed to restore session:", error);
        persistToken(null);
        clearUserState();
      });
    } else {
      persistToken(null);
      clearUserState();
    }
  }, [persistToken, populateUsers, clearUserState]);

  useEffect(() => {
    if (accessToken) {
      setPickerUsers([]);
      setPickerError(null);
      setPickerLoading(false);
      return;
    }

    let cancelled = false;
    setPickerLoading(true);
    setPickerError(null);

    http
      .get<ApiUser[]>("user/list")
      .then((response) => {
        if (cancelled) return;
        setPickerUsers(response.data.map(mapUser));
      })
      .catch((error) => {
        if (cancelled) return;
        // eslint-disable-next-line no-console
        console.error("[UserContext] Failed to load user list:", error);
        setPickerUsers([]);
        setPickerError("Failed to load users. Please try again.");
      })
      .finally(() => {
        if (cancelled) return;
        setPickerLoading(false);
      });

    return () => {
      cancelled = true;
    };
  }, [accessToken]);

  const selectUser = useCallback(
    (id: number | null) => {
      if (id == null) {
        persistToken(null);
        clearUserState();
        return;
      }

      (async () => {
        try {
          const response = await http.post<LoginResponse>("auth/impersonate", { userId: id });
          persistToken(response.data.accessToken);
          setUserIdState(response.data.user.id);
          setUsers([mapUser(response.data.user)]);
          qc.invalidateQueries();
        } catch (error) {
          // eslint-disable-next-line no-console
          console.error("[UserContext] Failed to impersonate user:", error);
          persistToken(null);
          clearUserState();
        }
      })();
    },
    [persistToken, clearUserState, qc]
  );

  const value = useMemo(
    () => ({ userId, setUserId: selectUser, users, refreshUsers }),
    [userId, users, selectUser, refreshUsers]
  );

  return (
    <UserContext.Provider value={value}>
      {children}
      {!accessToken ? (
        <div
          className="fixed inset-0 z-50 flex items-center justify-center bg-background/95 p-6"
          role="dialog"
          aria-modal="true"
          data-testid="user-picker"
        >
          <div className="w-full max-w-md space-y-4 rounded-lg border bg-card p-6 shadow-xl">
            <div className="space-y-2 text-center">
              <h2 className="text-xl font-semibold">Select a user</h2>
              <p className="text-sm text-muted-foreground">Choose an account to continue.</p>
            </div>
            {pickerError ? (
              <div className="rounded border border-destructive bg-destructive/10 p-3 text-sm text-destructive" role="alert">
                {pickerError}
              </div>
            ) : null}
            {pickerLoading ? (
              <div className="text-center text-sm text-muted-foreground">Loading users…</div>
            ) : pickerUsers.length > 0 ? (
              <ul className="space-y-2">
                {pickerUsers.map((user) => (
                  <li key={user.id}>
                    <button
                      type="button"
                      onClick={() => {
                        selectUser(user.id);
                      }}
                      data-testid={`user-option-${user.id}`}
                      className="flex w-full items-center justify-between rounded-lg border border-input bg-background px-4 py-2 text-left text-sm font-medium hover:bg-accent hover:text-accent-foreground"
                    >
                      <span>{user.name}</span>
                      {user.isAdmin ? (
                        <span className="rounded bg-primary/10 px-2 py-0.5 text-xs font-semibold text-primary">Admin</span>
                      ) : null}
                    </button>
                  </li>
                ))}
              </ul>
            ) : (
              <div className="text-center text-sm text-muted-foreground">No users available.</div>
            )}
          </div>
        </div>
      ) : null}
    </UserContext.Provider>
  );
}
