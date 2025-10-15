import { useEffect, useMemo, useState, useCallback } from "react";
import type { FormEvent } from "react";
import { useQueryClient } from "@tanstack/react-query";
import http, { setHttpAccessToken } from "@/lib/http";
import { mapUser } from "@/lib/mapUser";
import type { AdminUserApi, ApiUser, UserLite } from "@/types/user";
import { UserContext } from "./UserContext";
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogDescription } from "@/components/ui/dialog";

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
  const [loginUsername, setLoginUsername] = useState("");
  const [loginPassword, setLoginPassword] = useState("");
  const [loginError, setLoginError] = useState<string | null>(null);
  const [loginLoading, setLoginLoading] = useState(false);

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

  const handleLogin = useCallback(
    async (event?: FormEvent<HTMLFormElement>) => {
      event?.preventDefault();

      const username = loginUsername.trim();
      const password = loginPassword;

      if (!username || !password) {
        setLoginError("Username and password are required.");
        return;
      }

      setLoginLoading(true);
      setLoginError(null);

      try {
        const response = await http.post<LoginResponse>("auth/login", {
          username,
          password,
        });

        persistToken(response.data.accessToken);

        try {
          await populateUsers();
          qc.invalidateQueries();
        } catch (error) {
          // eslint-disable-next-line no-console
          console.error("[UserContext] Failed to populate user state after login:", error);
          persistToken(null);
          clearUserState();
          setLoginError("Failed to load user information.");
          return;
        }

        setLoginUsername("");
        setLoginPassword("");
      } catch (error) {
        // eslint-disable-next-line no-console
        console.error("[UserContext] Login failed:", error);
        setLoginError("Invalid username or password.");
      } finally {
        setLoginLoading(false);
      }
    },
    [loginUsername, loginPassword, persistToken, populateUsers, qc, clearUserState]
  );

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

  const isLoginModalOpen = !accessToken;

  // Disable body scroll when login modal is open
  useEffect(() => {
    if (isLoginModalOpen) {
      document.body.style.overflow = "hidden";
      return () => {
        document.body.style.overflow = "";
      };
    }
  }, [isLoginModalOpen]);

  return (
    <UserContext.Provider value={value}>
      {children}
      <Dialog open={isLoginModalOpen} onOpenChange={() => {}}>
        <DialogContent
          className="max-w-md"
          aria-labelledby="login-title"
          aria-describedby="login-description"
        >
          <div className="space-y-4 p-6" data-testid="user-picker">
            <DialogHeader className="border-0 p-0">
              <DialogTitle id="login-title" className="text-center">
                Sign in
              </DialogTitle>
              <DialogDescription id="login-description" className="text-center">
                Enter your credentials to continue.
              </DialogDescription>
            </DialogHeader>
            {loginError ? (
              <div className="rounded border border-destructive bg-destructive/10 p-3 text-sm text-destructive" role="alert">
                {loginError}
              </div>
            ) : null}
            <form className="space-y-3" onSubmit={handleLogin}>
              <div className="space-y-1">
                <label className="text-sm font-medium" htmlFor="login-username">
                  Username
                </label>
                <input
                  id="login-username"
                  type="text"
                  autoComplete="username"
                  value={loginUsername}
                  onChange={(event) => setLoginUsername(event.target.value)}
                  className="w-full rounded-md border border-input bg-background px-3 py-2 text-sm"
                />
              </div>
              <div className="space-y-1">
                <label className="text-sm font-medium" htmlFor="login-password">
                  Password
                </label>
                <input
                  id="login-password"
                  type="password"
                  autoComplete="current-password"
                  value={loginPassword}
                  onChange={(event) => setLoginPassword(event.target.value)}
                  className="w-full rounded-md border border-input bg-background px-3 py-2 text-sm"
                />
              </div>
              <button
                type="submit"
                className="w-full rounded-md bg-primary px-3 py-2 text-sm font-medium text-primary-foreground hover:bg-primary/90"
                disabled={loginLoading}
              >
                {loginLoading ? "Signing in…" : "Sign in"}
              </button>
            </form>
            {import.meta.env.DEV ? (
              <div className="text-xs text-muted-foreground text-center">
                Use seeded credentials as documented in your environment setup.
              </div>
            ) : null}
          </div>
        </DialogContent>
      </Dialog>
    </UserContext.Provider>
  );
}
