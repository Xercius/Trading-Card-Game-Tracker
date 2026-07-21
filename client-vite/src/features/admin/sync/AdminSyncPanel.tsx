import { useMemo, useState } from "react";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { getErrorMessage } from "@/lib/getErrorMessage";
import {
  useRunStarWarsUnlimitedSyncMutation,
  useStarWarsUnlimitedSyncStatusQuery,
  type AdminSyncStatusResponse,
} from "./api";

type SyncFeedback = {
  tone: "success" | "error";
  message: string;
};

function formatDateTime(value: string | null) {
  if (!value) {
    return "Never";
  }

  return new Date(value).toLocaleString();
}

function buildSuccessMessage(result: AdminSyncStatusResponse) {
  return `Sync completed — ${result.setCount} sets, ${result.created} created, ${result.updated} updated, ${result.invalid} invalid.`;
}

export default function AdminSyncPanel() {
  const statusQuery = useStarWarsUnlimitedSyncStatusQuery();
  const runSync = useRunStarWarsUnlimitedSyncMutation();
  const [feedback, setFeedback] = useState<SyncFeedback | null>(null);

  const latestHistory = useMemo(() => statusQuery.data?.history[0] ?? null, [statusQuery.data]);
  const isRunning = statusQuery.data?.status === "Running";

  const handleRunSync = async () => {
    setFeedback(null);

    try {
      const result = await runSync.mutateAsync();
      setFeedback({
        tone: "success",
        message: buildSuccessMessage(result),
      });
    } catch (error) {
      setFeedback({
        tone: "error",
        message: getErrorMessage(error, "Failed to run sync."),
      });
    }
  };

  if (statusQuery.isLoading) {
    return <section className="rounded border p-4">Loading sync status…</section>;
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
            Review the latest sync activity and trigger a fresh import.
          </p>
        </div>
        <Badge variant={isRunning ? "default" : "secondary"}>{statusQuery.data.status}</Badge>
      </div>

      <dl className="grid gap-4 md:grid-cols-3">
        <div className="space-y-1">
          <dt className="text-xs font-medium uppercase tracking-wide text-muted-foreground">
            Last completed
          </dt>
          <dd className="text-sm">{formatDateTime(statusQuery.data.lastCompletedAt)}</dd>
        </div>
        <div className="space-y-1">
          <dt className="text-xs font-medium uppercase tracking-wide text-muted-foreground">
            Latest set
          </dt>
          <dd className="text-sm">
            {latestHistory
              ? `${latestHistory.setCode} · ${formatDateTime(latestHistory.lastSyncedAt)}`
              : "No sets synced yet"}
          </dd>
        </div>
        <div className="space-y-1">
          <dt className="text-xs font-medium uppercase tracking-wide text-muted-foreground">
            Tracked sets
          </dt>
          <dd className="text-sm">{statusQuery.data.historyCount}</dd>
        </div>
      </dl>

      {statusQuery.data.messages.length > 0 && (
        <ul className="space-y-1 text-sm text-muted-foreground">
          {statusQuery.data.messages.map((message) => (
            <li key={message}>{message}</li>
          ))}
        </ul>
      )}

      {feedback && (
        <div
          className={`rounded border px-3 py-2 text-sm ${
            feedback.tone === "success"
              ? "border-emerald-500/40 bg-emerald-500/10 text-emerald-700"
              : "border-destructive/40 bg-destructive/10 text-destructive"
          }`}
        >
          {feedback.message}
        </div>
      )}

      <div className="flex flex-wrap gap-2">
        <Button type="button" onClick={handleRunSync} disabled={runSync.isPending || isRunning}>
          {runSync.isPending ? "Running sync…" : "Run sync"}
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
