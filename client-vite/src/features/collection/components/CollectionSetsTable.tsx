import React, { useState, useMemo } from "react";
import { useNavigate } from "react-router-dom";
import { useGame } from "@/state/useGame";
import { useCollectionSetsSummaryQuery } from "../api";
import { paths } from "@/routes/paths";

export const CollectionSetsTable: React.FC = () => {
  const { activeGame } = useGame();
  const navigate = useNavigate();
  const { data: summary, isLoading, error } = useCollectionSetsSummaryQuery(activeGame ?? undefined);

  const [expandedSets, setExpandedSets] = useState<Record<string, boolean>>({});
  const [filterQuery, setFilterQuery] = useState("");

  const toggleSet = (setCode: string, e: React.MouseEvent) => {
    e.stopPropagation();
    setExpandedSets((prev) => ({
      ...prev,
      [setCode]: !prev[setCode],
    }));
  };

  const toggleAll = (expand: boolean) => {
    if (!summary?.sets) return;
    const next: Record<string, boolean> = {};
    if (expand) {
      summary.sets.forEach((s) => {
        next[s.setCode] = true;
      });
    }
    setExpandedSets(next);
  };

  const filteredSets = useMemo(() => {
    if (!summary?.sets) return [];
    if (!filterQuery.trim()) return summary.sets;
    const q = filterQuery.toLowerCase();
    return summary.sets.filter(
      (s) =>
        s.setName.toLowerCase().includes(q) ||
        s.setCode.toLowerCase().includes(q)
    );
  }, [summary?.sets, filterQuery]);

  const handleSetClick = (setCode: string) => {
    navigate(`/${paths.collection}/set/${encodeURIComponent(setCode)}`);
  };

  const handleTreatmentClick = (setCode: string, treatmentName: string, e: React.MouseEvent) => {
    e.stopPropagation();
    navigate(`/${paths.collection}/set/${encodeURIComponent(setCode)}?treatment=${encodeURIComponent(treatmentName)}`);
  };

  if (isLoading) {
    return (
      <div className="flex h-48 items-center justify-center rounded-xl border border-zinc-800 bg-zinc-900/50 p-8 text-zinc-400">
        <div className="flex items-center gap-3">
          <span className="inline-block h-5 w-5 animate-spin rounded-full border-2 border-purple-500 border-t-transparent" />
          <span>Loading set summary for {activeGame || "all games"}…</span>
        </div>
      </div>
    );
  }

  if (error) {
    return (
      <div className="rounded-xl border border-red-900/40 bg-red-950/20 p-6 text-red-300">
        <p className="font-medium">Failed to load set summary.</p>
        <p className="mt-1 text-sm text-red-400">{(error as Error).message}</p>
      </div>
    );
  }

  if (!summary || summary.sets.length === 0) {
    return (
      <div className="rounded-xl border border-zinc-800 bg-zinc-900/50 p-8 text-center text-zinc-400">
        No sets found for {activeGame || "the selected game"}.
      </div>
    );
  }

  const allExpanded = summary.sets.length > 0 && summary.sets.every((s) => expandedSets[s.setCode]);

  return (
    <div className="space-y-4">
      {/* Overview Stats Bar */}
      <div className="flex flex-wrap items-center justify-between gap-4 rounded-xl border border-zinc-800 bg-zinc-900/70 p-4 backdrop-blur-sm">
        <div>
          <div className="text-xs uppercase tracking-wider text-zinc-400 font-semibold">
            {activeGame || "All Games"} Collection Progress
          </div>
          <div className="mt-1 flex items-baseline gap-2">
            <span className="text-2xl font-bold text-zinc-100">
              {summary.totalUniqueCardsOwned.toLocaleString()}
            </span>
            <span className="text-sm text-zinc-400">
              / {summary.totalUniqueCardsInGame.toLocaleString()} unique cards
            </span>
            <span className="ml-2 text-base font-semibold text-purple-400">
              {summary.overallCompletionPercentage.toFixed(1)}%
            </span>
          </div>
        </div>
        <div className="w-full max-w-xs sm:w-64">
          <div className="h-2.5 w-full overflow-hidden rounded-full bg-zinc-800">
            <div
              className="h-full bg-gradient-to-r from-purple-600 to-indigo-500 transition-all duration-500"
              style={{ width: `${Math.min(100, Math.max(0, summary.overallCompletionPercentage))}%` }}
            />
          </div>
        </div>
      </div>

      {/* Filter and Expand Controls */}
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div className="relative w-full max-w-sm">
          <input
            type="text"
            value={filterQuery}
            onChange={(e) => setFilterQuery(e.target.value)}
            placeholder="Filter sets by name or code..."
            className="w-full rounded-lg border border-zinc-700 bg-zinc-900 px-3.5 py-1.5 text-sm text-zinc-100 placeholder-zinc-500 shadow-sm focus:border-purple-500 focus:outline-none focus:ring-1 focus:ring-purple-500"
          />
          {filterQuery && (
            <button
              onClick={() => setFilterQuery("")}
              className="absolute right-2.5 top-1/2 -translate-y-1/2 text-xs text-zinc-400 hover:text-zinc-200"
            >
              ✕
            </button>
          )}
        </div>

        <button
          type="button"
          onClick={() => toggleAll(!allExpanded)}
          className="rounded-lg border border-zinc-700 bg-zinc-800/80 px-3 py-1.5 text-xs font-medium text-zinc-300 hover:bg-zinc-700 hover:text-white transition-colors"
        >
          {allExpanded ? "Collapse All Subsets" : "Expand All Subsets"}
        </button>
      </div>

      {/* Sets Table */}
      <div className="overflow-hidden rounded-xl border border-zinc-800 bg-zinc-900 shadow">
        <div className="overflow-x-auto">
          <table className="w-full text-left text-sm text-zinc-300">
            <thead className="border-b border-zinc-800 bg-zinc-950/70 text-xs uppercase tracking-wider text-zinc-400">
              <tr>
                <th scope="col" className="w-12 px-3 py-3 text-center">
                  <span className="sr-only">Expand</span>
                </th>
                <th scope="col" className="px-4 py-3 font-semibold">
                  Set Name
                </th>
                <th scope="col" className="w-24 px-4 py-3 font-semibold">
                  Code
                </th>
                <th scope="col" className="w-28 px-4 py-3 font-semibold text-center">
                  Year
                </th>
                <th scope="col" className="w-36 px-4 py-3 font-semibold text-right">
                  Unique Cards
                </th>
                <th scope="col" className="w-48 px-4 py-3 font-semibold">
                  Completion
                </th>
              </tr>
            </thead>
            <tbody className="divide-y divide-zinc-800/60">
              {filteredSets.map((set) => {
                const isExpanded = !!expandedSets[set.setCode];
                const hasSubsets = set.subsets && set.subsets.length > 0;

                return (
                  <React.Fragment key={set.setCode}>
                    {/* Main Set Row */}
                    <tr
                      onClick={() => handleSetClick(set.setCode)}
                      className="group cursor-pointer bg-zinc-900 hover:bg-zinc-800/70 transition-colors"
                    >
                      <td className="px-3 py-3 text-center">
                        {hasSubsets && (
                          <button
                            type="button"
                            onClick={(e) => toggleSet(set.setCode, e)}
                            className="inline-flex h-6 w-6 items-center justify-center rounded text-zinc-400 hover:bg-zinc-700 hover:text-zinc-100 transition-colors"
                            aria-label={isExpanded ? "Collapse subsets" : "Expand subsets"}
                          >
                            <span
                              className={`transform transition-transform text-xs font-bold ${
                                isExpanded ? "rotate-90" : "rotate-0"
                              }`}
                            >
                              ▶
                            </span>
                          </button>
                        )}
                      </td>
                      <td className="px-4 py-3 font-medium text-zinc-100">
                        <div className="flex items-center gap-2">
                          <span className="group-hover:text-purple-400 transition-colors">
                            {set.setName}
                          </span>
                          {set.isVirtualSet && (
                            <span className="rounded bg-amber-900/40 px-1.5 py-0.5 text-[10px] font-semibold text-amber-300 border border-amber-700/50">
                              Virtual
                            </span>
                          )}
                        </div>
                      </td>
                      <td className="px-4 py-3 font-mono text-xs text-zinc-400 font-semibold">
                        {set.setCode}
                      </td>
                      <td className="px-4 py-3 text-center text-zinc-400">
                        {set.releaseYear || "—"}
                      </td>
                      <td className="px-4 py-3 text-right font-mono text-zinc-200">
                        <span className="font-semibold text-zinc-100">{set.ownedCards}</span>
                        <span className="text-zinc-500"> / {set.totalCards}</span>
                      </td>
                      <td className="px-4 py-3">
                        <div className="flex items-center gap-3">
                          <div className="h-2 w-24 overflow-hidden rounded-full bg-zinc-800">
                            <div
                              className="h-full bg-purple-500 transition-all duration-300"
                              style={{ width: `${Math.min(100, Math.max(0, set.completionPercentage))}%` }}
                            />
                          </div>
                          <span className="w-14 text-right font-mono text-xs font-semibold text-zinc-200">
                            {set.completionPercentage.toFixed(1)}%
                          </span>
                        </div>
                      </td>
                    </tr>

                    {/* Expandable Treatment Subsets */}
                    {isExpanded &&
                      set.subsets.map((subset) => (
                        <tr
                          key={`${set.setCode}-${subset.treatmentName}`}
                          onClick={(e) => handleTreatmentClick(set.setCode, subset.treatmentName, e)}
                          className="cursor-pointer bg-zinc-950/50 hover:bg-zinc-800/40 border-l-2 border-l-purple-500/50 transition-colors"
                        >
                          <td className="px-3 py-2.5 text-center text-zinc-600">
                            <span className="text-xs">↳</span>
                          </td>
                          <td className="pl-6 pr-4 py-2.5 text-xs text-zinc-300">
                            <span className="inline-block rounded-md bg-zinc-800/90 border border-zinc-700/60 px-2 py-0.5 text-zinc-200 font-medium hover:border-purple-400 transition-colors">
                              {subset.treatmentName}
                            </span>
                          </td>
                          <td className="px-4 py-2.5 font-mono text-xs text-zinc-500">
                            —
                          </td>
                          <td className="px-4 py-2.5 text-center text-xs text-zinc-500">
                            —
                          </td>
                          <td className="px-4 py-2.5 text-right font-mono text-xs text-zinc-300">
                            <span className="font-semibold text-zinc-200">{subset.ownedCards}</span>
                            <span className="text-zinc-500"> / {subset.totalCards}</span>
                          </td>
                          <td className="px-4 py-2.5">
                            <div className="flex items-center gap-3">
                              <div className="h-1.5 w-24 overflow-hidden rounded-full bg-zinc-800">
                                <div
                                  className="h-full bg-purple-400/80 transition-all duration-300"
                                  style={{ width: `${Math.min(100, Math.max(0, subset.completionPercentage))}%` }}
                                />
                              </div>
                              <span className="w-14 text-right font-mono text-xs text-zinc-400">
                                {subset.completionPercentage.toFixed(1)}%
                              </span>
                            </div>
                          </td>
                        </tr>
                      ))}
                  </React.Fragment>
                );
              })}
            </tbody>
          </table>
        </div>
      </div>
    </div>
  );
};
