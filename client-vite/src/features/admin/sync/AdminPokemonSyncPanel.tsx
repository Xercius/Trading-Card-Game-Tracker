import { useMemo, useState } from "react";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import {
  isAdminSyncUnauthorized,
  useAdminSyncLoginMutation,
  useAdminSyncLogoutMutation,
  usePokemonSyncStatusQuery,
  useRunPokemonCardsSyncMutation,
  useRunPokemonSetsSyncMutation,
  type AdminPokemonSetEntry,
} from "./api";

type SyncFeedback = {
  tone: "success" | "error";
  message: string;
};

function formatDateTime(value: string | null) {
  return value ? new Date(value).toLocaleString() : "Never";
}

export default function AdminPokemonSyncPanel() {
  const statusQuery = usePokemonSyncStatusQuery();
  const runSetsSync = useRunPokemonSetsSyncMutation();
  const runCardsSync = useRunPokemonCardsSyncMutation();
  const login = useAdminSyncLoginMutation();
  const logout = useAdminSyncLogoutMutation();

  const [feedback, setFeedback] = useState<SyncFeedback | null>(null);
  const [dryRun, setDryRun] = useState(false);
  const [forceFullSync, setForceFullSync] = useState(false);
  const [filterText, setFilterText] = useState("");
  const [selectedSetCodes, setSelectedSetCodes] = useState<Set<string>>(new Set());
  const [expandedEras, setExpandedEras] = useState<Set<string>>(new Set());

  const isRunning = statusQuery.data?.isRunning || runSetsSync.isPending || runCardsSync.isPending;

  const sets = useMemo(() => statusQuery.data?.sets ?? [], [statusQuery.data?.sets]);

  // Group sets by era
  const groupedEras = useMemo(() => {
    const map = new Map<string, AdminPokemonSetEntry[]>();
    for (const s of sets) {
      const eraName = s.era?.trim() || "Other";
      if (!map.has(eraName)) {
        map.set(eraName, []);
      }
      map.get(eraName)!.push(s);
    }

    // Sort eras by latest release date of their sets (newest first)
    const sorted = Array.from(map.entries()).map(([era, eraSets]) => {
      // Sort sets within era by release date descending
      eraSets.sort((a, b) => {
        const dateA = a.releaseDate ?? "0000";
        const dateB = b.releaseDate ?? "0000";
        return dateB.localeCompare(dateA);
      });
      const latestDate = eraSets[0]?.releaseDate ?? "0000";
      return { era, sets: eraSets, latestDate };
    });

    sorted.sort((a, b) => b.latestDate.localeCompare(a.latestDate));
    return sorted;
  }, [sets]);

  // Filtered eras based on search input
  const filteredEras = useMemo(() => {
    if (!filterText.trim()) return groupedEras;
    const term = filterText.trim().toLowerCase();

    return groupedEras
      .map((group) => {
        const matchingSets = group.sets.filter(
          (s) =>
            s.name.toLowerCase().includes(term) ||
            s.code.toLowerCase().includes(term) ||
            group.era.toLowerCase().includes(term)
        );
        return { ...group, sets: matchingSets };
      })
      .filter((group) => group.sets.length > 0);
  }, [groupedEras, filterText]);

  // Automatically expand matching eras during search
  const isSearching = filterText.trim().length > 0;

  const toggleEraExpanded = (era: string) => {
    setExpandedEras((prev) => {
      const next = new Set(prev);
      if (next.has(era)) {
        next.delete(era);
      } else {
        next.add(era);
      }
      return next;
    });
  };

  const handleExpandAll = () => {
    setExpandedEras(new Set(groupedEras.map((g) => g.era)));
  };

  const handleCollapseAll = () => {
    setExpandedEras(new Set());
  };

  const toggleSetSelection = (code: string) => {
    setSelectedSetCodes((prev) => {
      const next = new Set(prev);
      if (next.has(code)) {
        next.delete(code);
      } else {
        next.add(code);
      }
      return next;
    });
  };

  const toggleEraSelection = (eraSets: AdminPokemonSetEntry[]) => {
    const unsyncedSets = eraSets.filter((s) => !s.isSynced);
    if (unsyncedSets.length === 0) return;

    const allUnsyncedSelected = unsyncedSets.every((s) => selectedSetCodes.has(s.code));

    setSelectedSetCodes((prev) => {
      const next = new Set(prev);
      if (allUnsyncedSelected) {
        // Deselect all unsynced in this era
        for (const s of unsyncedSets) {
          next.delete(s.code);
        }
      } else {
        // Select all unsynced in this era
        for (const s of unsyncedSets) {
          next.add(s.code);
        }
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
        message: result.messages.join(" ") || "Pokémon sets sync completed successfully.",
      });
    } catch {
      setFeedback({
        tone: "error",
        message:
          "Failed to sync Pokémon sets. Sign in again if your administrator session expired.",
      });
    }
  };

  const handleRunCardsSync = async () => {
    setFeedback(null);
    const codesToSync = Array.from(selectedSetCodes);
    if (codesToSync.length === 0) {
      setFeedback({
        tone: "error",
        message: "Please select at least one set to sync cards.",
      });
      return;
    }

    try {
      const result = await runCardsSync.mutateAsync({
        setCodes: codesToSync,
        forceFullSync,
        dryRun,
      });

      const detailMsg = result.messages.length > 0 ? `: ${result.messages.join(" ")}` : "";
      setFeedback({
        tone: result.errors === 0 ? "success" : "error",
        message:
          result.errors === 0
            ? `Pokémon cards sync completed — ${result.cardsCreated + result.cardsUpdated} cards, ${result.printingsCreated + result.printingsUpdated} printings.${detailMsg}`
            : `Pokémon cards sync finished with errors (${result.errors} errors)${detailMsg}`,
      });

      // Clear selection of newly synced sets
      setSelectedSetCodes((prev) => {
        const next = new Set(prev);
        for (const code of codesToSync) {
          next.delete(code);
        }
        return next;
      });
    } catch {
      setFeedback({
        tone: "error",
        message:
          "Failed to sync Pokémon cards. Sign in again if your administrator session expired.",
      });
    }
  };

  if (
    isAdminSyncUnauthorized(statusQuery.error) ||
    isAdminSyncUnauthorized(runSetsSync.error) ||
    isAdminSyncUnauthorized(runCardsSync.error)
  ) {
    return (
      <section className="space-y-3 rounded border p-4">
        <h2 className="text-lg font-semibold">Pokémon TCG sync</h2>
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
              runSetsSync.reset();
              runCardsSync.reset();
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
          <Button type="submit" disabled={login.isPending}>
            {login.isPending ? "Signing in…" : "Sign in"}
          </Button>
        </form>
      </section>
    );
  }

  const selectedCount = selectedSetCodes.size;
  const totalSets = sets.length;
  const syncedSetsCount = sets.filter((s) => s.isSynced).length;

  return (
    <section className="space-y-6 rounded border p-6 bg-card text-card-foreground">
      {/* Header & Status */}
      <div className="flex flex-col gap-2 sm:flex-row sm:items-center sm:justify-between">
        <div>
          <h2 className="text-xl font-bold tracking-tight">Pokémon TCG Sync</h2>
          <p className="text-sm text-muted-foreground">
            Synchronize expansions, physical printing treatments, and cards from TCGdex.
          </p>
        </div>
        <div className="flex items-center gap-2">
          <Badge variant={isRunning ? "default" : "outline"} className="text-sm">
            {isRunning ? "Sync in progress…" : "Idle"}
          </Badge>
          <Button
            variant="ghost"
            size="sm"
            onClick={() => logout.mutate()}
            disabled={logout.isPending || isRunning}
          >
            Sign out
          </Button>
        </div>
      </div>

      {/* Sync Timestamps & Metadata Card */}
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
            {syncedSetsCount} / {totalSets} sets synced
          </span>
        </div>
      </div>

      {statusQuery.isError && !isAdminSyncUnauthorized(statusQuery.error) && (
        <div
          role="status"
          className="rounded border border-red-600 bg-red-50 p-3 text-sm text-red-950 dark:bg-red-950/30 dark:text-red-200"
        >
          Failed to load Pokémon sync status: {statusQuery.error.message}
        </div>
      )}

      {feedback && (
        <div
          role="status"
          className={`rounded border p-3 text-sm ${
            feedback.tone === "success"
              ? "border-green-600 bg-green-50 text-green-950 dark:bg-green-950/30 dark:text-green-200"
              : "border-red-600 bg-red-50 text-red-950 dark:bg-red-950/30 dark:text-red-200"
          }`}
        >
          {feedback.message}
        </div>
      )}

      {/* Controls & Action Buttons */}
      <div className="flex flex-wrap items-center gap-4 pt-2">
        <Button
          onClick={handleRunSetsSync}
          disabled={isRunning}
          variant="outline"
          className="font-medium"
        >
          {runSetsSync.isPending ? "Syncing Sets…" : "Sync Sets"}
        </Button>

        <Button
          onClick={handleRunCardsSync}
          disabled={isRunning || selectedCount === 0}
          className="font-semibold"
        >
          {runCardsSync.isPending ? "Syncing Cards…" : `Sync Cards (${selectedCount} selected)`}
        </Button>

        <label className="flex items-center gap-2 text-sm cursor-pointer select-none">
          <input
            type="checkbox"
            className="rounded border-gray-300"
            checked={dryRun}
            onChange={(e) => setDryRun(e.target.checked)}
            disabled={isRunning}
          />
          <span>Dry run (preview only)</span>
        </label>

        <label className="flex items-center gap-2 text-sm cursor-pointer select-none">
          <input
            type="checkbox"
            className="rounded border-gray-300"
            checked={forceFullSync}
            onChange={(e) => setForceFullSync(e.target.checked)}
            disabled={isRunning}
          />
          <span>Force full reconciliation</span>
        </label>
      </div>

      {/* Search and Accordion Quick Controls */}
      <div className="flex flex-col sm:flex-row items-stretch sm:items-center justify-between gap-3 pt-2">
        <Input
          placeholder="Filter sets by name, code, or era..."
          value={filterText}
          onChange={(e) => setFilterText(e.target.value)}
          className="max-w-sm"
        />

        <div className="flex items-center gap-2">
          <Button variant="ghost" size="sm" onClick={handleExpandAll} className="text-xs">
            Expand All
          </Button>
          <span className="text-muted-foreground">|</span>
          <Button variant="ghost" size="sm" onClick={handleCollapseAll} className="text-xs">
            Collapse All
          </Button>
        </div>
      </div>

      {/* Eras Accordion List */}
      <div className="space-y-3">
        {totalSets === 0 && !statusQuery.isLoading && (
          <div className="text-center py-8 text-muted-foreground rounded border border-dashed p-6">
            <p className="font-medium">No Pokémon sets found in database.</p>
            <p className="text-sm mt-1">
              Click <strong>"Sync Sets"</strong> above to fetch all expansions and eras from TCGdex.
            </p>
          </div>
        )}

        {filteredEras.map((group) => {
          const isExpanded = isSearching || expandedEras.has(group.era);
          const unsyncedSets = group.sets.filter((s) => !s.isSynced);
          const syncedSets = group.sets.filter((s) => s.isSynced);
          const allSynced = unsyncedSets.length === 0;
          const someSelected =
            unsyncedSets.length > 0 &&
            unsyncedSets.some((s) => selectedSetCodes.has(s.code)) &&
            !unsyncedSets.every((s) => selectedSetCodes.has(s.code));
          const allUnsyncedSelected =
            unsyncedSets.length > 0 && unsyncedSets.every((s) => selectedSetCodes.has(s.code));

          return (
            <div
              key={group.era}
              className="rounded-lg border bg-card/60 overflow-hidden transition-colors"
            >
              {/* Era Header */}
              <div className="flex items-center justify-between p-3.5 bg-muted/30 hover:bg-muted/50 cursor-pointer select-none">
                <div className="flex items-center gap-3">
                  {/* Master Checkbox for Era */}
                  <input
                    type="checkbox"
                    className="h-4 w-4 rounded border-gray-300 text-primary cursor-pointer disabled:opacity-60"
                    checked={allSynced || allUnsyncedSelected}
                    ref={(el) => {
                      if (el) el.indeterminate = someSelected;
                    }}
                    disabled={allSynced || isRunning}
                    onChange={(e) => {
                      e.stopPropagation();
                      toggleEraSelection(group.sets);
                    }}
                    title={
                      allSynced
                        ? "All sets in this era are already synced"
                        : "Select or deselect all unsynced sets in this era"
                    }
                  />

                  {/* Toggle Expansion Chevron */}
                  <div
                    className="flex items-center gap-2"
                    onClick={() => toggleEraExpanded(group.era)}
                  >
                    <svg
                      className={`h-4 w-4 transition-transform duration-200 text-muted-foreground ${
                        isExpanded ? "rotate-90" : ""
                      }`}
                      fill="none"
                      viewBox="0 0 24 24"
                      stroke="currentColor"
                    >
                      <path
                        strokeLinecap="round"
                        strokeLinejoin="round"
                        strokeWidth={2}
                        d="M9 5l7 7-7 7"
                      />
                    </svg>

                    <span className="font-semibold text-base">{group.era}</span>
                    <span className="text-xs text-muted-foreground">
                      ({group.sets.length} {group.sets.length === 1 ? "set" : "sets"})
                    </span>
                  </div>
                </div>

                <div
                  className="flex items-center gap-2"
                  onClick={() => toggleEraExpanded(group.era)}
                >
                  <span className="text-xs text-muted-foreground">
                    {syncedSets.length} / {group.sets.length} synced
                  </span>
                  {allSynced && (
                    <Badge
                      variant="secondary"
                      className="bg-green-100 text-green-800 dark:bg-green-950 dark:text-green-300 text-xs"
                    >
                      Complete
                    </Badge>
                  )}
                </div>
              </div>

              {/* Expanded Sets Table */}
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
                      {group.sets.map((set) => {
                        const isSelected = selectedSetCodes.has(set.code);

                        return (
                          <tr
                            key={set.code}
                            className={`hover:bg-muted/40 ${set.isSynced ? "bg-muted/15" : ""}`}
                          >
                            <td className="p-2">
                              {set.isSynced ? (
                                <input
                                  type="checkbox"
                                  checked={true}
                                  disabled={true}
                                  className="h-4 w-4 rounded border-gray-300 opacity-60 cursor-not-allowed"
                                  title="Synced sets cannot be unchecked"
                                />
                              ) : (
                                <input
                                  type="checkbox"
                                  checked={isSelected}
                                  onChange={() => toggleSetSelection(set.code)}
                                  disabled={isRunning}
                                  className="h-4 w-4 rounded border-gray-300 text-primary cursor-pointer"
                                />
                              )}
                            </td>
                            <td className="p-2 font-mono text-xs text-muted-foreground">
                              {set.releaseDate ?? "—"}
                            </td>
                            <td className="p-2 font-mono font-medium text-xs">
                              <Badge variant="outline">{set.code}</Badge>
                            </td>
                            <td className="p-2 font-medium">{set.name}</td>
                            <td className="p-2 font-mono text-xs">
                              {set.cardCountInDb > 0 ? (
                                <span className="font-semibold">{set.cardCountInDb}</span>
                              ) : (
                                <span className="text-muted-foreground">0</span>
                              )}
                            </td>
                            <td className="p-2 text-xs text-muted-foreground">
                              {formatDateTime(set.lastSyncedAt)}
                            </td>
                            <td className="p-2">
                              {set.isSynced ? (
                                <Badge className="bg-green-600 hover:bg-green-600 text-white text-xs">
                                  Synced
                                </Badge>
                              ) : isSelected ? (
                                <Badge variant="secondary" className="text-xs">
                                  Ready to sync
                                </Badge>
                              ) : (
                                <Badge variant="outline" className="text-muted-foreground text-xs">
                                  Not imported
                                </Badge>
                              )}
                            </td>
                          </tr>
                        );
                      })}
                    </tbody>
                  </table>
                </div>
              )}
            </div>
          );
        })}
      </div>
    </section>
  );
}
