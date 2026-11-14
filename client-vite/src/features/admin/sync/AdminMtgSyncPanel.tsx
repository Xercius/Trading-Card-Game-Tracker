import { useMemo, useState } from "react";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import {
  useMtgSyncStatusQuery,
  useRunMtgCardsSyncMutation,
  useRunMtgSetsSyncMutation,
  type AdminMtgSetEntry,
} from "./api";

type SyncFeedback = {
  tone: "success" | "error";
  message: string;
};

type GroupedEra = {
  era: string;
  sets: AdminMtgSetEntry[];
  latestDate: string;
};

function formatDateTime(value: string | null) {
  return value ? new Date(value).toLocaleString() : "Never";
}

function MtgEraAccordionGroup({
  group,
  isExpanded,
  isRunning,
  selectedSetCodes,
  onToggleExpand,
  onToggleEraSelection,
  onToggleSetSelection,
}: {
  group: GroupedEra;
  isExpanded: boolean;
  isRunning: boolean;
  selectedSetCodes: Set<string>;
  onToggleExpand: (era: string) => void;
  onToggleEraSelection: (sets: AdminMtgSetEntry[]) => void;
  onToggleSetSelection: (code: string) => void;
}) {
  const unsyncedSets = group.sets.filter((s) => !s.isSynced);
  const syncedCount = group.sets.length - unsyncedSets.length;
  const allSynced = unsyncedSets.length === 0;
  const targetSets = allSynced ? group.sets : unsyncedSets;
  const allTargetSelected =
    targetSets.length > 0 && targetSets.every((s) => selectedSetCodes.has(s.code));
  const someSelected =
    targetSets.length > 0 &&
    targetSets.some((s) => selectedSetCodes.has(s.code)) &&
    !allTargetSelected;

  return (
    <div className="rounded-lg border bg-card/60 overflow-hidden">
      <div className="flex items-center justify-between p-3.5 bg-muted/30 hover:bg-muted/50">
        <div className="flex items-center gap-3">
          <input
            type="checkbox"
            className="h-4 w-4 rounded border-gray-300 cursor-pointer disabled:opacity-60"
            checked={allTargetSelected}
            ref={(el) => {
              if (el) el.indeterminate = someSelected;
            }}
            disabled={isRunning}
            onChange={() => onToggleEraSelection(group.sets)}
            aria-label={
              allSynced ? `Select all sets in ${group.era}` : `Select unsynced sets in ${group.era}`
            }
          />
          <button
            type="button"
            className="flex items-center gap-2 text-left"
            onClick={() => onToggleExpand(group.era)}
            aria-expanded={isExpanded}
          >
            <span className="font-semibold text-base">{group.era}</span>
            <span className="text-xs text-muted-foreground">({group.sets.length} sets)</span>
          </button>
        </div>
        <div className="flex items-center gap-2">
          <span className="text-xs text-muted-foreground">
            {syncedCount} / {group.sets.length} synced
          </span>
          {allSynced && <Badge variant="secondary">Complete</Badge>}
        </div>
      </div>

      {isExpanded && (
        <div className="p-3 border-t overflow-x-auto">
          <table className="w-full text-left text-sm">
            <thead>
              <tr className="border-b text-xs text-muted-foreground">
                <th className="p-2 w-8">Import</th>
                <th className="p-2">Release Date</th>
                <th className="p-2">Code</th>
                <th className="p-2">Set Name</th>
                <th className="p-2">Cards in DB</th>
                <th className="p-2">Last Synced</th>
                <th className="p-2">Status</th>
              </tr>
            </thead>
            <tbody className="divide-y">
              {group.sets.map((set) => (
                <tr key={set.code} className={set.isSynced ? "bg-muted/15" : "hover:bg-muted/40"}>
                  <td className="p-2">
                    <input
                      type="checkbox"
                      checked={selectedSetCodes.has(set.code)}
                      disabled={isRunning}
                      onChange={() => onToggleSetSelection(set.code)}
                      aria-label={`Select ${set.name}`}
                    />
                  </td>
                  <td className="p-2 whitespace-nowrap">{set.releaseDate ?? "—"}</td>
                  <td className="p-2 font-mono text-xs">{set.code}</td>
                  <td className="p-2 font-medium">{set.name}</td>
                  <td className="p-2">{set.cardCountInDb}</td>
                  <td className="p-2 text-xs text-muted-foreground">
                    {formatDateTime(set.lastSyncedAt)}
                  </td>
                  <td className="p-2">
                    <Badge variant={set.isSynced ? "secondary" : "outline"}>
                      {set.isSynced ? "Synced" : "Pending"}
                    </Badge>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
}

/**
 * Admin panel implementing the two-step Magic: The Gathering Scryfall sync workflow.
 */
export default function AdminMtgSyncPanel() {
  const statusQuery = useMtgSyncStatusQuery();
  const runSetsSync = useRunMtgSetsSyncMutation();
  const runCardsSync = useRunMtgCardsSyncMutation();

  const [feedback, setFeedback] = useState<SyncFeedback | null>(null);
  const [dryRun, setDryRun] = useState(false);
  const [forceFullSync, setForceFullSync] = useState(false);
  const [filterText, setFilterText] = useState("");
  const [selectedSetCodes, setSelectedSetCodes] = useState<Set<string>>(new Set());
  const [expandedEras, setExpandedEras] = useState<Set<string>>(new Set());

  const isRunning = statusQuery.data?.isRunning || runSetsSync.isPending || runCardsSync.isPending;
  const sets = useMemo(() => statusQuery.data?.sets ?? [], [statusQuery.data?.sets]);

  const groupedEras = useMemo<GroupedEra[]>(() => {
    const map = new Map<string, AdminMtgSetEntry[]>();
    for (const s of sets) {
      const eraName = s.era?.trim() || "Other";
      const list = map.get(eraName) ?? [];
      list.push(s);
      map.set(eraName, list);
    }

    const sorted = Array.from(map.entries()).map(([era, eraSets]) => {
      eraSets.sort((a, b) => (b.releaseDate ?? "0000").localeCompare(a.releaseDate ?? "0000"));
      return { era, sets: eraSets, latestDate: eraSets[0]?.releaseDate ?? "0000" };
    });
    sorted.sort((a, b) => b.latestDate.localeCompare(a.latestDate));
    return sorted;
  }, [sets]);

  const filteredEras = useMemo(() => {
    const term = filterText.trim().toLowerCase();
    if (!term) return groupedEras;
    return groupedEras
      .map((group) => ({
        ...group,
        sets: group.sets.filter(
          (s) =>
            s.name.toLowerCase().includes(term) ||
            s.code.toLowerCase().includes(term) ||
            group.era.toLowerCase().includes(term)
        ),
      }))
      .filter((group) => group.sets.length > 0);
  }, [groupedEras, filterText]);

  const isSearching = filterText.trim().length > 0;
  const syncedSetsCount = sets.filter((s) => s.isSynced).length;

  const toggleSetSelection = (code: string) => {
    setSelectedSetCodes((prev) => {
      const next = new Set(prev);
      if (next.has(code)) next.delete(code);
      else next.add(code);
      return next;
    });
  };

  const toggleEraSelection = (eraSets: AdminMtgSetEntry[]) => {
    const unsynced = eraSets.filter((s) => !s.isSynced);
    const targets = unsynced.length > 0 ? unsynced : eraSets;
    if (targets.length === 0) return;
    const allSelected = targets.every((s) => selectedSetCodes.has(s.code));
    setSelectedSetCodes((prev) => {
      const next = new Set(prev);
      for (const s of targets) {
        if (allSelected) next.delete(s.code);
        else next.add(s.code);
      }
      return next;
    });
  };

  const handleRunSetsSync = async () => {
    setFeedback(null);
    try {
      const result = await runSetsSync.mutateAsync();
      setFeedback({
        tone: "success",
        message: result.messages.join(" ") || "MTG sets sync completed successfully.",
      });
    } catch {
      setFeedback({ tone: "error", message: "Failed to sync MTG sets." });
    }
  };

  const handleRunCardsSync = async () => {
    setFeedback(null);
    const codes = Array.from(selectedSetCodes);
    if (codes.length === 0) {
      setFeedback({ tone: "error", message: "Please select at least one set to sync cards." });
      return;
    }

    try {
      const result = await runCardsSync.mutateAsync({
        setCodes: codes,
        forceFullSync,
        dryRun,
      });
      setFeedback({
        tone: result.errors === 0 ? "success" : "error",
        message:
          result.errors === 0
            ? `MTG cards sync completed — ${result.cardsCreated + result.cardsUpdated} cards, ${result.printingsCreated + result.printingsUpdated} printings.`
            : `MTG cards sync finished with ${result.errors} errors: ${result.messages.join(" ")}`,
      });
      if (!dryRun && result.errors === 0) {
        setSelectedSetCodes(new Set());
      }
    } catch {
      setFeedback({ tone: "error", message: "Failed to sync MTG cards." });
    }
  };

  return (
    <section className="space-y-6 rounded border p-6 bg-card text-card-foreground">
      <div className="flex flex-col gap-2 sm:flex-row sm:items-center sm:justify-between">
        <div>
          <h2 className="text-xl font-bold tracking-tight">Magic: The Gathering Sync</h2>
          <p className="text-sm text-muted-foreground">
            Synchronize physical MTG sets, cards, and finishes from Scryfall.
          </p>
        </div>
        <Badge variant={isRunning ? "default" : "outline"}>
          {isRunning ? "Sync in progress…" : "Idle"}
        </Badge>
      </div>

      <div className="grid grid-cols-1 sm:grid-cols-3 gap-4 rounded-lg bg-muted/40 p-4 border text-sm">
        <div>
          <span className="font-semibold text-muted-foreground block">Last Sets Sync:</span>
          <span>{formatDateTime(statusQuery.data?.lastSetsSyncAt ?? null)}</span>
        </div>
        <div>
          <span className="font-semibold text-muted-foreground block">Last Cards Sync:</span>
          <span>{formatDateTime(statusQuery.data?.lastCardsSyncAt ?? null)}</span>
        </div>
        <div>
          <span className="font-semibold text-muted-foreground block">Set Coverage:</span>
          <span>
            {syncedSetsCount} / {sets.length} sets synced
          </span>
        </div>
      </div>

      {statusQuery.isError && (
        <div role="status" className="rounded border border-red-600 bg-red-50 p-3 text-sm text-red-950">
          Failed to load MTG sync status: {statusQuery.error.message}
        </div>
      )}

      {feedback && (
        <div
          role="status"
          className={`rounded border p-3 text-sm ${
            feedback.tone === "success"
              ? "border-green-600 bg-green-50 text-green-950"
              : "border-red-600 bg-red-50 text-red-950"
          }`}
        >
          {feedback.message}
        </div>
      )}

      <div className="flex flex-wrap items-center gap-4">
        <Button onClick={handleRunSetsSync} disabled={isRunning} variant="outline">
          {runSetsSync.isPending ? "Syncing Sets…" : "Sync Sets"}
        </Button>
        <Button onClick={handleRunCardsSync} disabled={isRunning || selectedSetCodes.size === 0}>
          {runCardsSync.isPending
            ? "Syncing Cards…"
            : `Sync Cards (${selectedSetCodes.size} selected)`}
        </Button>
        <label className="flex items-center gap-2 text-sm cursor-pointer">
          <input
            type="checkbox"
            checked={dryRun}
            onChange={(e) => setDryRun(e.target.checked)}
            disabled={isRunning}
          />
          <span>Dry run (preview only)</span>
        </label>
        <label className="flex items-center gap-2 text-sm cursor-pointer">
          <input
            type="checkbox"
            checked={forceFullSync}
            onChange={(e) => setForceFullSync(e.target.checked)}
            disabled={isRunning}
          />
          <span>Force full reconciliation</span>
        </label>
      </div>

      <div className="flex flex-col sm:flex-row items-stretch sm:items-center justify-between gap-3">
        <Input
          placeholder="Filter sets by name, code, or category..."
          value={filterText}
          onChange={(e) => setFilterText(e.target.value)}
          className="max-w-sm"
        />
        <div className="flex items-center gap-2">
          <Button
            variant="ghost"
            size="sm"
            onClick={() => setExpandedEras(new Set(groupedEras.map((g) => g.era)))}
          >
            Expand All
          </Button>
          <Button variant="ghost" size="sm" onClick={() => setExpandedEras(new Set())}>
            Collapse All
          </Button>
        </div>
      </div>

      <div className="space-y-3">
        {sets.length === 0 && !statusQuery.isLoading && (
          <div className="text-center py-8 text-muted-foreground rounded border border-dashed p-6">
            <p className="font-medium">No MTG sets found in database.</p>
            <p className="text-sm mt-1">
              Click <strong>&quot;Sync Sets&quot;</strong> above to fetch sets from Scryfall.
            </p>
          </div>
        )}
        {filteredEras.map((group) => (
          <MtgEraAccordionGroup
            key={group.era}
            group={group}
            isExpanded={isSearching || expandedEras.has(group.era)}
            isRunning={isRunning}
            selectedSetCodes={selectedSetCodes}
            onToggleExpand={(era) =>
              setExpandedEras((prev) => {
                const next = new Set(prev);
                if (next.has(era)) next.delete(era);
                else next.add(era);
                return next;
              })
            }
            onToggleEraSelection={toggleEraSelection}
            onToggleSetSelection={toggleSetSelection}
          />
        ))}
      </div>
    </section>
  );
}
