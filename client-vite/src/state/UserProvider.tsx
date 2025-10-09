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
  const [pickerUsers, setPickerUsers] = useState<UserLite[]>([]);
  const [pickerError, setPickerError] = useState<string | null>(null);
  const [pickerLoading, setPickerLoading] = useState(false);

  const applyUserId = useCallback((id: number | null) => {
    setUserIdState(id);
    setHttpUserId(id);
    if (typeof window !== "undefined") {
      const storage = window.localStorage;
      if (id != null) storage.setItem("userId", String(id));
      else storage.removeItem("userId");
    }
  }, []);

  const refreshUsersInternal = useCallback(async (id: number | null) => {
    if (id == null) {
      setUsers([]);
      return;
    }

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

  const refreshUsers = useCallback(async () => {
    await refreshUsersInternal(userId);
  }, [refreshUsersInternal, userId]);

  useEffect(() => {
    const saved = typeof window !== "undefined" ? window.localStorage.getItem("userId") : null;
    const parsed = saved ? Number(saved) : NaN;
    if (Number.isInteger(parsed) && parsed > 0) {
      applyUserId(parsed);
      void refreshUsersInternal(parsed);
    } else {
      applyUserId(null);
      setUsers([]);
    }
  }, [applyUserId, refreshUsersInternal]);

  useEffect(() => {
    if (userId != null) {
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
  }, [userId]);

  const selectUser = useCallback(
    (id: number) => {
      applyUserId(id);
      qc.invalidateQueries();
      void refreshUsersInternal(id);
    },
    [applyUserId, qc, refreshUsersInternal]
  );

  const setUserId = useCallback(
    (id: number) => {
      selectUser(id);
    },
    [selectUser]
  );

  const value = useMemo(
    () => ({ userId, setUserId, users, refreshUsers }),
    [userId, users, setUserId, refreshUsers]
  );

  return (
    <UserContext.Provider value={value}>
      {children}
      {userId == null ? (
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
