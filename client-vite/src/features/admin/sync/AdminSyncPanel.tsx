import { useState } from "react";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import {
  isAdminSyncUnauthorized,
  useAdminSyncLoginMutation,
  useAdminSyncLogoutMutation,
  useRunStarWarsUnlimitedSyncMutation,
  useStarWarsUnlimitedSyncStatusQuery,
} from "./api";

type SyncFeedback = {
  tone: "success" | "error";
  message: string;
};

function formatDateTime(value: string | null) {
  return value ? new Date(value).toLocaleString() : "Never";
}

export default function AdminSyncPanel() {
  const statusQuery = useStarWarsUnlimitedSyncStatusQuery();
  const runSync = useRunStarWarsUnlimitedSyncMutation();
  const login = useAdminSyncLoginMutation();
  const logout = useAdminSyncLogoutMutation();
  const [feedback, setFeedback] = useState<SyncFeedback | null>(null);

  const isRunning = statusQuery.data?.status === "Running" || runSync.isPending;
  const syncStatus = isRunning ? "Running" : statusQuery.data?.status;
  const progressMessage = isRunning
    ? runSync.isPending && runSync.variables
      ? "Full reconciliation is running…"
      : "Sync is running…"
    : "";

  const handleRunSync = async (forceFullSync = false) => {
    setFeedback(null);

    try {
      const result = await runSync.mutateAsync(forceFullSync);
      const succeeded = result.status === "Succeeded" && result.invalid === 0;
      setFeedback({
        tone: succeeded ? "success" : "error",
        message: succeeded
          ? `Sync completed — ${result.setCount} sets, ${result.created} created, ${result.updated} updated, ${result.invalid} invalid.`
          : `Sync incomplete — ${result.invalid} invalid. The last successful global checkpoint is unchanged; retry the sync.`,
      });
    } catch {
      setFeedback({
        tone: "error",
        message:
          "Failed to run sync. The last successful global checkpoint is unchanged. Sign in again if your session expired.",
      });
    }
  };

  if (isAdminSyncUnauthorized(statusQuery.error) || isAdminSyncUnauthorized(runSync.error)) {
    return (
      <section className="space-y-3 rounded border p-4">
        <h2 className="text-lg font-semibold">Star Wars Unlimited sync</h2>
        <p>Sign in with an administrator account to manage sync.</p>
        <form
          className="space-y-3"
          onSubmit={async (event) => {
            event.preventDefault();
            const form = event.currentTarget;
            const data = new FormData(form);
            try {
              await login.mutateAsync({
                username: String(data.get("username") ?? ""),
                password: String(data.get("password") ?? ""),
              });
              form.reset();
              runSync.reset();
              setFeedback(null);
            } catch {
              form.reset();
            }
          }}
        >
          <label className="block">
            Username
            <input
              className="block rounded border p-2"
              name="username"
              autoComplete="username"
              required
              maxLength={200}
            />
          </label>
          <label className="block">
            Password
            <input
              className="block rounded border p-2"
              name="password"
              type="password"
              autoComplete="current-password"
              required
              maxLength={1024}
            />
          </label>
          {login.isError && (
            <p role="alert">
              Sign-in failed. Check your administrator credentials or try again later.
            </p>
          )}
          <Button type="submit" disabled={login.isPending}>
            {login.isPending ? "Signing in…" : "Sign in"}
          </Button>
        </form>
      </section>
    );
  }

  if (statusQuery.isLoading) {
    return (
      <section className="rounded border p-4" role="status" aria-live="polite">
        Loading sync status…
      </section>
    );
  }

  if (statusQuery.isError || !statusQuery.data) {
    return (
      <section className="space-y-3 rounded border p-4">
        <div>
          <h2 className="text-lg font-semibold">Star Wars Unlimited sync</h2>
          <p className="text-sm text-muted-foreground">
            Load the latest card data from the remote source.
          </p>
        </div>
        <div className="rounded border border-destructive/40 bg-destructive/10 px-3 py-2 text-sm text-destructive">
          Failed to load sync status.
        </div>
        <Button type="button" variant="outline" onClick={() => statusQuery.refetch()}>
          Retry
        </Button>
      </section>
    );
  }

  return (
    <section className="space-y-4 rounded border p-4">
      <div className="flex flex-col gap-3 md:flex-row md:items-start md:justify-between">
        <div>
          <h2 className="text-lg font-semibold">Star Wars Unlimited sync</h2>
          <p className="text-sm text-muted-foreground">
            Sync changes since the last successful run, or reconcile all cards with a full sync.
          </p>
        </div>
        <Badge variant={isRunning ? "default" : "secondary"}>{syncStatus}</Badge>
      </div>

      <div
        className={progressMessage ? "rounded border px-3 py-2 text-sm" : "sr-only"}
        role="status"
        aria-label="Sync progress"
        aria-live="polite"
      >
        {progressMessage}
      </div>

      <dl className="grid gap-4 md:grid-cols-3">
        <div className="space-y-1">
          <dt className="text-xs font-medium uppercase tracking-wide text-muted-foreground">
            Last successful global sync
          </dt>
          <dd className="text-sm">{formatDateTime(statusQuery.data.lastCompletedAt)}</dd>
        </div>
        <div className="space-y-1">
          <dt className="text-xs font-medium uppercase tracking-wide text-muted-foreground">
            Last outcome
          </dt>
          <dd className="text-sm">{statusQuery.data.lastOutcome ?? "Not run yet"}</dd>
        </div>
      </dl>

      {statusQuery.data.messages.length > 0 && (
        <ul className="space-y-1 text-sm text-muted-foreground">
          {statusQuery.data.messages.map((message) => (
            <li key={message}>{message}</li>
          ))}
        </ul>
      )}

      <div
        className={
          feedback?.tone === "success"
            ? "rounded border border-emerald-500/40 bg-emerald-500/10 px-3 py-2 text-sm text-emerald-700"
            : "sr-only"
        }
        role="status"
        aria-label="Sync outcome"
        aria-live="polite"
      >
        {feedback?.tone === "success" ? feedback.message : ""}
      </div>
      <div
        className={
          feedback?.tone === "error"
            ? "rounded border border-destructive/40 bg-destructive/10 px-3 py-2 text-sm text-destructive"
            : "sr-only"
        }
        role="alert"
        aria-label="Sync error"
        aria-live="assertive"
      >
        {feedback?.tone === "error" ? feedback.message : ""}
      </div>

      <div className="flex flex-wrap gap-2">
        <Button
          type="button"
          variant="outline"
          onClick={() => logout.mutate()}
          disabled={logout.isPending || isRunning}
        >
          Sign out
        </Button>
        {logout.isError && <p role="alert">Sign-out failed. Try again.</p>}
        <Button
          type="button"
          onClick={() => handleRunSync()}
          disabled={runSync.isPending || isRunning}
        >
          {runSync.isPending ? "Running sync…" : "Run sync"}
        </Button>
        <Button
          type="button"
          variant="outline"
          onClick={() => handleRunSync(true)}
          disabled={runSync.isPending || isRunning}
        >
          Full reconciliation
        </Button>
        <Button
          type="button"
          variant="outline"
          onClick={() => statusQuery.refetch()}
          disabled={statusQuery.isFetching || runSync.isPending}
        >
          Refresh
        </Button>
      </div>
    </section>
  );
}
