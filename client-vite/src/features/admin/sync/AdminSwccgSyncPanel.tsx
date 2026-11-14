import { useState } from "react";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import {
  useRunStarWarsCcgSyncMutation,
  useStarWarsCcgSyncStatusQuery,
  type SwccgSyncOptions,
} from "./api";

type SyncFeedback = {
  tone: "success" | "error";
  message: string;
};

function formatDateTime(value: string | null) {
  return value ? new Date(value).toLocaleString() : "Never";
}

export default function AdminSwccgSyncPanel() {
  const statusQuery = useStarWarsCcgSyncStatusQuery();
  const runSync = useRunStarWarsCcgSyncMutation();
  const [feedback, setFeedback] = useState<SyncFeedback | null>(null);
  const [dryRun, setDryRun] = useState(false);

  const isRunning = statusQuery.data?.status === "Running" || runSync.isPending;
  const syncStatus = isRunning ? "Running" : statusQuery.data?.status;
  const progressMessage = isRunning ? "SWCCG sync is running…" : "";

  const handleRunSync = async (overrideOptions?: SwccgSyncOptions) => {
    setFeedback(null);

    const options: SwccgSyncOptions = overrideOptions ?? {
      dryRun,
    };

    try {
      const result = await runSync.mutateAsync(options);
      const succeeded = result.status === "Succeeded" && result.invalid === 0;
      const detailMsg =
        result.messages && result.messages.length > 0 ? `: ${result.messages.join(" ")}` : "";
      setFeedback({
        tone: succeeded ? "success" : "error",
        message: succeeded
          ? `SWCCG sync completed — ${result.setCount} sets, ${result.created} created, ${result.updated} updated, ${result.invalid} invalid.`
          : `SWCCG sync completed with warnings or invalid cards (${result.invalid} invalid)${detailMsg}`,
      });
    } catch {
      setFeedback({
        tone: "error",
        message: "Failed to run SWCCG sync.",
      });
    }
  };

  if (statusQuery.isLoading) {
    return (
      <section className="rounded border p-4" role="status" aria-live="polite">
        Loading SWCCG sync status…
      </section>
    );
  }

  if (statusQuery.isError || !statusQuery.data) {
    return (
      <section className="space-y-3 rounded border p-4">
        <div>
          <h2 className="text-lg font-semibold">Star Wars CCG sync</h2>
          <p className="text-sm text-muted-foreground">
            Load card data and images from the swccg-card-json and Holotable repositories.
          </p>
        </div>
        <div className="rounded border border-destructive/40 bg-destructive/10 px-3 py-2 text-sm text-destructive">
          Failed to load SWCCG sync status.
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
          <h2 className="text-lg font-semibold">Star Wars CCG sync</h2>
          <p className="text-sm text-muted-foreground">
            Sync original and virtual SWCCG cards from the swccg-card-json catalog and Holotable
            card images.
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

      {/* Options */}
      <div className="flex flex-wrap items-center gap-5 pt-2">
        <label className="flex items-center gap-2 text-sm cursor-pointer select-none">
          <input
            type="checkbox"
            checked={dryRun}
            onChange={(e) => setDryRun(e.target.checked)}
            disabled={isRunning}
            className="rounded border"
          />
          <span>Dry run (validate without committing changes)</span>
        </label>
      </div>

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
          onClick={() => handleRunSync()}
          disabled={runSync.isPending || isRunning}
        >
          {runSync.isPending ? "Running sync…" : "Run SWCCG sync"}
        </Button>
        <Button
          type="button"
          variant="outline"
          onClick={() => handleRunSync({ dryRun: true })}
          disabled={runSync.isPending || isRunning}
        >
          Dry run preview
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
