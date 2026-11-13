import { useState, useMemo, useEffect } from "react";
import { useParams, useSearchParams, Link } from "react-router-dom";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { useGame } from "@/state/useGame";
import { useUser } from "@/state/useUser";
import { queryKeys } from "@/lib/queryKeys";
import { fetchAllPrintings } from "@/features/printings/api/printings";
import {
  fetchAllCollection,
  useSetCardQuantitiesMutation,
  useCollectionSetsSummaryQuery,
  type CollectionItem,
} from "@/features/collection/api";
import { CardImageHoverPreview } from "@/components/ui/CardImageHoverPreview";
import { isHorizontalPrinting } from "@/features/printings/utils/cardOrientation";
import EditCardModal from "@/features/collection/EditCardModal";
import { paths } from "@/routes/paths";

interface PrintingRowData {
  printingId: number;
  cardId: number;
  cardName: string;
  number: string;
  numericNumber: number;
  treatment: string;
  rarity: string;
  imageUrl: string | null;
  isHorizontal: boolean;
  owned: number;
  proxy: number;
  wanted: number;
}

export default function CollectionSetDetailPage() {
  const { setCode } = useParams<{ setCode: string }>();
  const [searchParams, setSearchParams] = useSearchParams();
  const { activeGame } = useGame();
  const { userId } = useUser();
  const queryClient = useQueryClient();

  // Query parameter for treatment
  const treatmentParam = searchParams.get("treatment") || "All";
  const [selectedTreatment, setSelectedTreatment] = useState(treatmentParam);
  const [searchTerm, setSearchTerm] = useState("");
  const [selectedRarity, setSelectedRarity] = useState("All");
  const [hideFullyOwned, setHideFullyOwned] = useState(false);

  // Modal editing state
  const [editingCard, setEditingCard] = useState<CollectionItem | null>(null);

  // Sync state with URL params
  useEffect(() => {
    const fromUrl = searchParams.get("treatment") || "All";
    setSelectedTreatment(fromUrl);
  }, [searchParams]);

  const handleSelectTreatment = (treatment: string) => {
    setSelectedTreatment(treatment);
    setSearchParams((prev) => {
      const next = new URLSearchParams(prev);
      if (treatment === "All") {
        next.delete("treatment");
      } else {
        next.set("treatment", treatment);
      }
      return next;
    });
  };

  // 1. Fetch set summary to display set metadata & completion
  const { data: summaryData } = useCollectionSetsSummaryQuery(activeGame ?? undefined);
  const currentSetSummary = useMemo(() => {
    if (!summaryData?.sets || !setCode) return null;
    return summaryData.sets.find((s) => s.setCode.toLowerCase() === setCode.toLowerCase()) || null;
  }, [summaryData, setCode]);

  // 2. Fetch all printings for this set
  const {
    data: printingsData,
    isLoading: printingsLoading,
    error: printingsError,
    isSuccess: printingsLoaded,
  } = useQuery({
    queryKey: queryKeys.printings.set(setCode, activeGame),
    queryFn: async () => {
      if (!setCode) return [];
      return fetchAllPrintings({
        set: [setCode],
        game: activeGame ? [activeGame] : undefined,
      });
    },
    enabled: !!setCode,
  });

  // 3. Fetch user's collection for this set
  const collectionQueryKey = queryKeys.collection.setDetail(setCode, userId, activeGame);
  const {
    data: collectionData,
    isLoading: collectionLoading,
    error: collectionError,
    isSuccess: collectionLoaded,
  } = useQuery({
    queryKey: collectionQueryKey,
    queryFn: async () => {
      if (!userId || !setCode) return [];
      return fetchAllCollection({
        userId,
        page: 1,
        filters: { set: setCode, game: activeGame ?? undefined },
        includeProxies: true,
      });
    },
    enabled: !!userId && !!setCode,
  });

  // Quantities mutation
  const setQuantitiesMutation = useSetCardQuantitiesMutation();

  // Combine printings with collection counts
  const rows: PrintingRowData[] = useMemo(() => {
    if (!printingsData) return [];
    const colMap = new Map<number, CollectionItem>();
    if (collectionData) {
      for (const item of collectionData) {
        colMap.set(item.cardPrintingId, item);
      }
    }

    const result: PrintingRowData[] = printingsData.map((p) => {
      const pid = Number(p.printingId);
      const userCard = colMap.get(pid);
      const numStr = p.number ?? "—";
      const parsedNum = parseInt(numStr.replace(/\D/g, ""), 10);
      const isHorizontal = isHorizontalPrinting(p);

      return {
        printingId: pid,
        cardId: Number(p.cardId),
        cardName: p.cardName,
        number: numStr,
        numericNumber: Number.isFinite(parsedNum) ? parsedNum : 9999,
        treatment: p.style || "Standard",
        rarity: p.rarity || "—",
        imageUrl: p.imageUrl ?? null,
        isHorizontal,
        owned: userCard?.quantityOwned ?? 0,
        proxy: userCard?.quantityProxyOwned ?? 0,
        wanted: userCard?.quantityWanted ?? 0,
      };
    });

    // Natural collector number sort
    result.sort((a, b) => a.numericNumber - b.numericNumber || a.number.localeCompare(b.number));
    return result;
  }, [printingsData, collectionData]);

  // Extract distinct treatments and rarities
  const availableTreatments = useMemo(() => {
    const set = new Set<string>();
    rows.forEach((r) => set.add(r.treatment));
    return ["All", ...Array.from(set).sort()];
  }, [rows]);

  const availableRarities = useMemo(() => {
    const set = new Set<string>();
    rows.forEach((r) => {
      if (r.rarity && r.rarity !== "—") set.add(r.rarity);
    });
    return ["All", ...Array.from(set).sort()];
  }, [rows]);

  // Filter rows
  const filteredRows = useMemo(() => {
    let list = rows;

    if (selectedTreatment !== "All") {
      list = list.filter((r) => r.treatment.toLowerCase() === selectedTreatment.toLowerCase());
    }

    if (selectedRarity !== "All") {
      list = list.filter((r) => r.rarity.toLowerCase() === selectedRarity.toLowerCase());
    }

    if (searchTerm.trim()) {
      const q = searchTerm.toLowerCase();
      list = list.filter(
        (r) =>
          r.cardName.toLowerCase().includes(q) ||
          r.number.toLowerCase().includes(q) ||
          r.treatment.toLowerCase().includes(q)
      );
    }

    if (hideFullyOwned) {
      list = list.filter((r) => r.owned === 0);
    }

    return list;
  }, [rows, selectedTreatment, selectedRarity, searchTerm, hideFullyOwned]);

  // Handler for stepper quantity updates
  const handleUpdateQty = (
    row: PrintingRowData,
    field: "owned" | "proxy" | "wanted",
    newVal: number
  ) => {
    if (!collectionLoaded || setQuantitiesMutation.isPending) return;
    const clamped = Math.max(0, Math.min(999, newVal));
    const nextOwned = field === "owned" ? clamped : row.owned;
    const nextProxy = field === "proxy" ? clamped : row.proxy;
    const nextWanted = field === "wanted" ? clamped : row.wanted;

    setQuantitiesMutation.mutate(
      {
        printingId: row.printingId,
        quantityOwned: nextOwned,
        quantityProxyOwned: nextProxy,
        quantityWanted: nextWanted,
      },
      {
        onSuccess: async () => {
          await queryClient.invalidateQueries({ queryKey: collectionQueryKey });
        },
      }
    );
  };

  const handleOpenEditModal = (row: PrintingRowData) => {
    const colItem: CollectionItem = {
      cardPrintingId: row.printingId,
      cardId: row.cardId,
      cardName: row.cardName,
      game: activeGame ?? "",
      set: setCode ?? "",
      number: row.number,
      rarity: row.rarity,
      style: row.treatment,
      imageUrl: row.imageUrl,
      quantityOwned: row.owned,
      quantityProxyOwned: row.proxy,
      quantityWanted: row.wanted,
      availability: row.owned,
      availabilityWithProxies: row.owned + row.proxy,
    };
    setEditingCard(colItem);
  };

  const isLoading = printingsLoading || collectionLoading;

  return (
    <div className="mx-auto max-w-7xl space-y-6 px-4 py-6">
      {/* Top Breadcrumb */}
      <div className="flex items-center gap-2 text-sm text-zinc-400">
        <Link
          to={`/${paths.collection}`}
          className="inline-flex items-center gap-1.5 font-medium text-purple-400 hover:text-purple-300 transition-colors"
        >
          <span>← Back to Sets Overview</span>
        </Link>
        <span>/</span>
        <span className="font-semibold text-zinc-200">{setCode?.toUpperCase()}</span>
      </div>

      {/* Set Header Summary */}
      <div className="flex flex-wrap items-center justify-between gap-6 rounded-2xl border border-zinc-800 bg-zinc-900/80 p-6 shadow-md backdrop-blur-md">
        <div>
          <div className="flex items-center gap-3">
            <h1 className="text-2xl font-bold tracking-tight text-zinc-100 sm:text-3xl">
              {currentSetSummary?.setName || setCode?.toUpperCase()}
            </h1>
            <span className="rounded-md border border-zinc-700 bg-zinc-800 px-2 py-0.5 font-mono text-xs font-semibold text-zinc-300">
              {setCode?.toUpperCase()}
            </span>
            {currentSetSummary?.isVirtualSet && (
              <span className="rounded bg-amber-900/40 px-2 py-0.5 text-xs font-semibold text-amber-300 border border-amber-700/50">
                Virtual Set
              </span>
            )}
          </div>
          <p className="mt-1 text-sm text-zinc-400">
            {activeGame || "Star Wars Unlimited"} • Released:{" "}
            {currentSetSummary?.releaseYear || "Unknown"}
          </p>
        </div>

        {currentSetSummary && (
          <div className="flex items-center gap-6">
            <div className="text-right">
              <div className="text-xs uppercase tracking-wider text-zinc-400 font-semibold">
                Set Completion
              </div>
              <div className="mt-0.5 flex items-baseline justify-end gap-1.5 font-mono text-zinc-200">
                <span className="text-xl font-bold text-zinc-100">
                  {currentSetSummary.ownedCards}
                </span>
                <span className="text-zinc-500">/ {currentSetSummary.totalCards} cards</span>
                <span className="ml-1 text-sm font-semibold text-purple-400">
                  ({currentSetSummary.completionPercentage.toFixed(1)}%)
                </span>
              </div>
            </div>
            <div className="h-3 w-32 overflow-hidden rounded-full bg-zinc-800">
              <div
                className="h-full bg-gradient-to-r from-purple-500 to-indigo-500 transition-all duration-300"
                style={{
                  width: `${Math.min(100, Math.max(0, currentSetSummary.completionPercentage))}%`,
                }}
              />
            </div>
          </div>
        )}
      </div>

      {/* Filter and Treatments Navigation Pills */}
      <div className="space-y-3 rounded-xl border border-zinc-800 bg-zinc-900/60 p-4">
        {/* Treatment Pills */}
        <div>
          <div className="text-xs font-semibold uppercase tracking-wider text-zinc-400 mb-2">
            Treatment Style
          </div>
          <div className="flex flex-wrap gap-1.5">
            {availableTreatments.map((treatment) => {
              const active = selectedTreatment.toLowerCase() === treatment.toLowerCase();
              return (
                <button
                  key={treatment}
                  type="button"
                  onClick={() => handleSelectTreatment(treatment)}
                  className={`rounded-lg px-3 py-1 text-xs font-medium transition-all ${
                    active
                      ? "bg-purple-600 text-white shadow-sm ring-1 ring-purple-400"
                      : "bg-zinc-800/80 text-zinc-400 hover:bg-zinc-700/80 hover:text-zinc-200 border border-zinc-700/50"
                  }`}
                >
                  {treatment}
                </button>
              );
            })}
          </div>
        </div>

        {/* Secondary Filter Row: Search, Rarity, Hide Owned */}
        <div className="flex flex-wrap items-center justify-between gap-3 pt-2 border-t border-zinc-800/70">
          <div className="flex flex-wrap items-center gap-3 w-full sm:w-auto">
            {/* Search */}
            <div className="relative w-full max-w-xs">
              <input
                type="text"
                value={searchTerm}
                onChange={(e) => setSearchTerm(e.target.value)}
                placeholder="Search card name or #..."
                className="w-full rounded-lg border border-zinc-700 bg-zinc-900 px-3 py-1.5 text-xs text-zinc-100 placeholder-zinc-500 shadow-sm focus:border-purple-500 focus:outline-none focus:ring-1 focus:ring-purple-500"
              />
              {searchTerm && (
                <button
                  onClick={() => setSearchTerm("")}
                  className="absolute right-2 top-1/2 -translate-y-1/2 text-xs text-zinc-400 hover:text-zinc-200"
                >
                  ✕
                </button>
              )}
            </div>

            {/* Rarity Filter */}
            {availableRarities.length > 2 && (
              <div className="flex items-center gap-1.5">
                <span className="text-xs text-zinc-400 font-medium">Rarity:</span>
                <select
                  value={selectedRarity}
                  onChange={(e) => setSelectedRarity(e.target.value)}
                  className="rounded-lg border border-zinc-700 bg-zinc-900 px-2.5 py-1.5 text-xs text-zinc-200 focus:border-purple-500 focus:outline-none"
                >
                  {availableRarities.map((r) => (
                    <option key={r} value={r}>
                      {r}
                    </option>
                  ))}
                </select>
              </div>
            )}
          </div>

          {/* Toggle: Hide Owned */}
          <label className="flex items-center gap-2 cursor-pointer text-xs text-zinc-300 select-none">
            <input
              type="checkbox"
              checked={hideFullyOwned}
              onChange={(e) => setHideFullyOwned(e.target.checked)}
              className="h-4 w-4 rounded border-zinc-700 bg-zinc-800 text-purple-600 focus:ring-purple-500"
            />
            <span>Show only unowned cards</span>
          </label>
        </div>
      </div>

      {/* Cards Table */}
      {isLoading ? (
        <div className="flex h-64 items-center justify-center rounded-xl border border-zinc-800 bg-zinc-900/50 p-8 text-zinc-400">
          <div className="flex items-center gap-3">
            <span className="inline-block h-5 w-5 animate-spin rounded-full border-2 border-purple-500 border-t-transparent" />
            <span>Loading printings for {setCode}…</span>
          </div>
        </div>
      ) : printingsError ? (
        <div className="rounded-xl border border-red-900/40 bg-red-950/20 p-6 text-red-300">
          <p className="font-medium">Failed to load printings for this set.</p>
          <p className="mt-1 text-sm text-red-400">{(printingsError as Error).message}</p>
        </div>
      ) : collectionError ? (
        <div className="rounded-xl border border-red-900/40 bg-red-950/20 p-6 text-red-300">
          <p className="font-medium">Failed to load collection quantities for this set.</p>
          <p className="mt-1 text-sm text-red-400">{(collectionError as Error).message}</p>
        </div>
      ) : filteredRows.length === 0 ? (
        <div className="rounded-xl border border-zinc-800 bg-zinc-900/50 p-12 text-center text-zinc-400">
          No cards match your current filter criteria.
        </div>
      ) : (
        <div className="overflow-hidden rounded-xl border border-zinc-800 bg-zinc-900 shadow">
          <div className="overflow-x-auto">
            <table className="w-full text-left text-sm text-zinc-300">
              <thead className="border-b border-zinc-800 bg-zinc-950/70 text-xs uppercase tracking-wider text-zinc-400">
                <tr>
                  <th scope="col" className="w-16 px-2 py-3 text-center font-semibold">
                    Image
                  </th>
                  <th scope="col" className="w-16 px-3 py-3 text-center font-semibold">
                    #
                  </th>
                  <th scope="col" className="px-4 py-3 font-semibold">
                    Card Name
                  </th>
                  <th scope="col" className="w-32 px-3 py-3 font-semibold">
                    Treatment
                  </th>
                  <th scope="col" className="w-24 px-3 py-3 font-semibold">
                    Rarity
                  </th>
                  <th scope="col" className="w-28 px-3 py-3 text-center font-semibold">
                    Owned
                  </th>
                  <th scope="col" className="w-28 px-3 py-3 text-center font-semibold">
                    Proxy
                  </th>
                  <th scope="col" className="w-28 px-3 py-3 text-center font-semibold">
                    Wishlist
                  </th>
                  <th scope="col" className="w-16 px-3 py-3 text-center font-semibold">
                    Edit
                  </th>
                </tr>
              </thead>
              <tbody className="divide-y divide-zinc-800/60">
                {filteredRows.map((row) => (
                  <tr
                    key={row.printingId}
                    onClick={() => handleOpenEditModal(row)}
                    className="cursor-pointer bg-zinc-900 hover:bg-zinc-800/60 transition-colors group"
                  >
                    {/* Thumbnail with Hover Zoom */}
                    <td className="px-2 py-2 text-center" onClick={(e) => e.stopPropagation()}>
                      <div className="flex items-center justify-center w-12 h-11 mx-auto">
                        <CardImageHoverPreview
                          imageUrl={row.imageUrl}
                          cardName={row.cardName}
                          isHorizontal={row.isHorizontal}
                        />
                      </div>
                    </td>

                    {/* Collector Number */}
                    <td className="px-3 py-2 text-center font-mono text-xs font-semibold text-zinc-400">
                      {row.number}
                    </td>

                    {/* Card Name */}
                    <td className="px-4 py-2 font-medium text-zinc-100 group-hover:text-purple-400 transition-colors">
                      {row.cardName}
                    </td>

                    {/* Treatment */}
                    <td className="px-3 py-2 text-xs">
                      <span className="inline-block rounded bg-zinc-800 border border-zinc-700/60 px-2 py-0.5 text-zinc-300 font-medium">
                        {row.treatment}
                      </span>
                    </td>

                    {/* Rarity */}
                    <td className="px-3 py-2 text-xs text-zinc-400 font-medium">{row.rarity}</td>

                    {/* Owned Quantity Stepper */}
                    <td className="px-2 py-2 text-center" onClick={(e) => e.stopPropagation()}>
                      <div className="inline-flex items-center rounded-lg border border-zinc-700/70 bg-zinc-950/80 p-0.5 shadow-inner">
                        <button
                          type="button"
                          onClick={() => handleUpdateQty(row, "owned", row.owned - 1)}
                          disabled={
                            !printingsLoaded ||
                            !collectionLoaded ||
                            setQuantitiesMutation.isPending ||
                            row.owned <= 0
                          }
                          className="h-6 w-6 rounded text-zinc-400 hover:bg-zinc-800 hover:text-white disabled:opacity-30 disabled:hover:bg-transparent"
                        >
                          −
                        </button>
                        <input
                          type="number"
                          min={0}
                          max={999}
                          value={row.owned}
                          disabled={
                            !printingsLoaded || !collectionLoaded || setQuantitiesMutation.isPending
                          }
                          onChange={(e) =>
                            handleUpdateQty(row, "owned", parseInt(e.target.value, 10) || 0)
                          }
                          className="w-8 text-center text-xs font-bold text-zinc-100 bg-transparent focus:outline-none disabled:opacity-50 [appearance:textfield] [&::-webkit-inner-spin-button]:appearance-none [&::-webkit-outer-spin-button]:appearance-none"
                        />
                        <button
                          type="button"
                          onClick={() => handleUpdateQty(row, "owned", row.owned + 1)}
                          disabled={
                            !printingsLoaded || !collectionLoaded || setQuantitiesMutation.isPending
                          }
                          className="h-6 w-6 rounded text-zinc-400 hover:bg-zinc-800 hover:text-white"
                        >
                          +
                        </button>
                      </div>
                    </td>

                    {/* Proxy Quantity Stepper */}
                    <td className="px-2 py-2 text-center" onClick={(e) => e.stopPropagation()}>
                      <div className="inline-flex items-center rounded-lg border border-zinc-700/70 bg-zinc-950/80 p-0.5 shadow-inner">
                        <button
                          type="button"
                          onClick={() => handleUpdateQty(row, "proxy", row.proxy - 1)}
                          disabled={
                            !printingsLoaded ||
                            !collectionLoaded ||
                            setQuantitiesMutation.isPending ||
                            row.proxy <= 0
                          }
                          className="h-6 w-6 rounded text-zinc-400 hover:bg-zinc-800 hover:text-white disabled:opacity-30 disabled:hover:bg-transparent"
                        >
                          −
                        </button>
                        <input
                          type="number"
                          min={0}
                          max={999}
                          value={row.proxy}
                          disabled={
                            !printingsLoaded || !collectionLoaded || setQuantitiesMutation.isPending
                          }
                          onChange={(e) =>
                            handleUpdateQty(row, "proxy", parseInt(e.target.value, 10) || 0)
                          }
                          className="w-8 text-center text-xs font-bold text-amber-300 bg-transparent focus:outline-none disabled:opacity-50 [appearance:textfield] [&::-webkit-inner-spin-button]:appearance-none [&::-webkit-outer-spin-button]:appearance-none"
                        />
                        <button
                          type="button"
                          onClick={() => handleUpdateQty(row, "proxy", row.proxy + 1)}
                          disabled={
                            !printingsLoaded || !collectionLoaded || setQuantitiesMutation.isPending
                          }
                          className="h-6 w-6 rounded text-zinc-400 hover:bg-zinc-800 hover:text-white"
                        >
                          +
                        </button>
                      </div>
                    </td>

                    {/* Wishlist Quantity Stepper */}
                    <td className="px-2 py-2 text-center" onClick={(e) => e.stopPropagation()}>
                      <div className="inline-flex items-center rounded-lg border border-zinc-700/70 bg-zinc-950/80 p-0.5 shadow-inner">
                        <button
                          type="button"
                          onClick={() => handleUpdateQty(row, "wanted", row.wanted - 1)}
                          disabled={
                            !printingsLoaded ||
                            !collectionLoaded ||
                            setQuantitiesMutation.isPending ||
                            row.wanted <= 0
                          }
                          className="h-6 w-6 rounded text-zinc-400 hover:bg-zinc-800 hover:text-white disabled:opacity-30 disabled:hover:bg-transparent"
                        >
                          −
                        </button>
                        <input
                          type="number"
                          min={0}
                          max={999}
                          value={row.wanted}
                          disabled={
                            !printingsLoaded || !collectionLoaded || setQuantitiesMutation.isPending
                          }
                          onChange={(e) =>
                            handleUpdateQty(row, "wanted", parseInt(e.target.value, 10) || 0)
                          }
                          className="w-8 text-center text-xs font-bold text-indigo-300 bg-transparent focus:outline-none disabled:opacity-50 [appearance:textfield] [&::-webkit-inner-spin-button]:appearance-none [&::-webkit-outer-spin-button]:appearance-none"
                        />
                        <button
                          type="button"
                          onClick={() => handleUpdateQty(row, "wanted", row.wanted + 1)}
                          disabled={
                            !printingsLoaded || !collectionLoaded || setQuantitiesMutation.isPending
                          }
                          className="h-6 w-6 rounded text-zinc-400 hover:bg-zinc-800 hover:text-white"
                        >
                          +
                        </button>
                      </div>
                    </td>

                    {/* Edit Modal Button */}
                    <td className="px-3 py-2 text-center" onClick={(e) => e.stopPropagation()}>
                      <button
                        type="button"
                        onClick={() => handleOpenEditModal(row)}
                        title="Edit collection entry"
                        className="rounded p-1 text-xs text-zinc-400 hover:bg-zinc-700 hover:text-white transition-colors"
                      >
                        ✎
                      </button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>
      )}

      {/* Edit Collection Card Modal */}
      {editingCard && (
        <EditCardModal
          open={!!editingCard}
          onOpenChange={(open) => {
            if (!open) {
              setEditingCard(null);
              // Invalidate query on close to refresh row values if changed
              queryClient.invalidateQueries({
                queryKey: queryKeys.collection.setDetailPrefix(setCode),
              });
            }
          }}
          item={editingCard}
          mode="collection"
        />
      )}
    </div>
  );
}
