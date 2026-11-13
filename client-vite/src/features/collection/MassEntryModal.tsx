import { useState, useEffect, useRef, useMemo } from "react";
import { Dialog, DialogContent, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import http from "@/lib/http";
import { fetchAllPrintings, type PrintingListItem } from "@/features/printings/api/printings";
import { useBatchSetCollectionMutation, fetchAllCollection, type CollectionItem } from "./api";
import { useUser } from "@/state/useUser";
import { useGame } from "@/state/useGame";

type MassEntryModalProps = {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  onSuccess?: (message: string) => void;
  game?: string;
};

type SetOption = {
  value: string;
  label: string;
  code?: string;
};

type CardRow = {
  printingId: number;
  number: string;
  numericNumber: number;
  cardName: string;
  rarity: string;
  owned: number;
  proxy: number;
  wanted: number;
  origOwned: number;
  origProxy: number;
  origWanted: number;
};

export default function MassEntryModal({
  open,
  onOpenChange,
  onSuccess,
  game,
}: MassEntryModalProps) {
  const { userId } = useUser();
  const { activeGame } = useGame();
  const currentGame = game || activeGame || "Star Wars Unlimited";

  const [sets, setSets] = useState<SetOption[]>([]);
  const [selectedSet, setSelectedSet] = useState<string>("");
  const [rows, setRows] = useState<CardRow[]>([]);
  const [isLoading, setIsLoading] = useState(false);
  const [isReady, setIsReady] = useState(false);
  const [loadError, setLoadError] = useState(false);
  const tableRef = useRef<HTMLTableElement>(null);

  const batchMutation = useBatchSetCollectionMutation();

  // Load sets for the active game when modal opens
  useEffect(() => {
    if (!open) return;
    http
      .get("cards/facets", {
        params: { game: currentGame },
      })
      .then((res) => {
        const rawSets = (res.data?.sets ?? res.data?.Sets ?? []) as unknown[];
        const setList: SetOption[] = rawSets
          .map((s) => {
            if (typeof s === "string") {
              return { value: s, label: s };
            }
            if (typeof s === "object" && s !== null) {
              const obj = s as { value?: string; label?: string; code?: string; name?: string };
              const val = obj.value || obj.code || obj.name || obj.label || "";
              const lbl = obj.label || obj.name || obj.value || obj.code || "";
              const code =
                obj.code || (obj.value && obj.value !== obj.label ? obj.value : undefined);
              return { value: val, label: lbl, code };
            }
            return null;
          })
          .filter((s): s is SetOption => !!s && !!s.value && !!s.label);

        setSets(setList);
        if (setList.length > 0) {
          setSelectedSet((prev) => {
            // Keep previous if still valid in the new game's sets, otherwise default to first set
            return prev && setList.some((s) => s.value === prev) ? prev : setList[0].value;
          });
        } else {
          setSelectedSet("");
          setRows([]);
        }
      })
      .catch((err) => console.error("[MassEntryModal] Failed to load sets", err));
  }, [open, currentGame]);

  // Load cards for selected set
  useEffect(() => {
    if (!open || !selectedSet) {
      setRows([]);
      setIsReady(false);
      return;
    }

    let isCancelled = false;
    setRows([]);
    setIsLoading(true);
    setIsReady(false);
    setLoadError(false);

    Promise.all([
      fetchAllPrintings({
        set: [selectedSet],
        game: currentGame ? [currentGame] : undefined,
      }),
      userId
        ? fetchAllCollection({
            userId,
            page: 1,
            filters: { set: selectedSet, game: currentGame },
            includeProxies: true,
          })
        : Promise.reject(new Error("User not selected")),
    ])
      .then(([printingsList, collectionList]) => {
        if (isCancelled) return;

        const collectionMap = new Map<number, CollectionItem>();
        for (const item of collectionList) {
          collectionMap.set(item.cardPrintingId, item);
        }

        const newRows: CardRow[] = printingsList.map((p: PrintingListItem) => {
          const printingId = Number(p.printingId);
          const userCard = collectionMap.get(printingId);

          const owned = userCard?.quantityOwned ?? 0;
          const proxy = userCard?.quantityProxyOwned ?? 0;
          const wanted = userCard?.quantityWanted ?? 0;

          const numStr = p.number ?? "999";
          const parsedNum = parseInt(numStr.replace(/\D/g, ""), 10);

          return {
            printingId,
            number: numStr,
            numericNumber: Number.isFinite(parsedNum) ? parsedNum : 999,
            cardName: p.cardName,
            rarity: p.rarity ?? "—",
            owned,
            proxy,
            wanted,
            origOwned: owned,
            origProxy: proxy,
            origWanted: wanted,
          };
        });

        // Sort by collector number
        newRows.sort(
          (a, b) => a.numericNumber - b.numericNumber || a.number.localeCompare(b.number)
        );
        setRows(newRows);
        setIsReady(true);
      })
      .catch((err) => {
        if (!isCancelled) {
          console.error("[MassEntryModal] Failed to load cards for set", err);
          setRows([]);
          setIsReady(false);
          setLoadError(true);
        }
      })
      .finally(() => {
        if (!isCancelled) setIsLoading(false);
      });

    return () => {
      isCancelled = true;
    };
  }, [open, selectedSet, userId, currentGame]);

  const modifiedRows = useMemo(() => {
    return rows.filter(
      (r) => r.owned !== r.origOwned || r.proxy !== r.origProxy || r.wanted !== r.origWanted
    );
  }, [rows]);

  const handleCellChange = (index: number, field: "owned" | "proxy" | "wanted", value: string) => {
    if (!isReady || batchMutation.isPending) return;
    const parsed = parseInt(value, 10);
    const validQty = Number.isFinite(parsed) && parsed >= 0 ? parsed : 0;

    setRows((prev) => {
      const copy = [...prev];
      copy[index] = { ...copy[index], [field]: validQty };
      return copy;
    });
  };

  // Excel-like keyboard navigation
  const handleKeyDown = (
    e: React.KeyboardEvent<HTMLInputElement>,
    rowIndex: number,
    colIndex: number
  ) => {
    if (e.key === "ArrowDown" || e.key === "Enter") {
      e.preventDefault();
      focusCell(rowIndex + 1, colIndex);
    } else if (e.key === "ArrowUp") {
      e.preventDefault();
      focusCell(rowIndex - 1, colIndex);
    } else if (e.key === "ArrowRight") {
      const input = e.currentTarget;
      if (input.selectionEnd === input.value.length) {
        e.preventDefault();
        focusCell(rowIndex, colIndex + 1);
      }
    } else if (e.key === "ArrowLeft") {
      const input = e.currentTarget;
      if (input.selectionStart === 0) {
        e.preventDefault();
        focusCell(rowIndex, colIndex - 1);
      }
    }
  };

  const focusCell = (targetRow: number, targetCol: number) => {
    if (!tableRef.current) return;
    const safeRow = Math.max(0, Math.min(rows.length - 1, targetRow));
    const safeCol = Math.max(0, Math.min(2, targetCol));
    const nextInput = tableRef.current.querySelector<HTMLInputElement>(
      `input[data-row="${safeRow}"][data-col="${safeCol}"]`
    );
    if (nextInput) {
      nextInput.focus();
      nextInput.select();
    }
  };

  const handleSaveAll = async () => {
    if (!isReady) return;
    if (modifiedRows.length === 0) {
      onOpenChange(false);
      return;
    }

    try {
      const payload = modifiedRows.map((r) => ({
        cardPrintingId: r.printingId,
        quantityOwned: r.owned,
        quantityProxyOwned: r.proxy,
        quantityWanted: r.wanted,
      }));

      await batchMutation.mutateAsync(payload);
      onSuccess?.(`Updated ${modifiedRows.length} card(s) in collection`);
      onOpenChange(false);
    } catch (err) {
      console.error("[MassEntryModal] Failed to save batch collection updates", err);
    }
  };

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-w-4xl max-h-[90vh] flex flex-col bg-[#242424] border border-white/20 p-6 text-card-foreground">
        <DialogHeader className="mb-2 shrink-0">
          <DialogTitle className="text-xl font-bold">Mass Entry (Set Spreadsheet)</DialogTitle>
          <p className="text-xs text-muted-foreground">
            Select a set to review and quickly input Owned, Proxy, and Wishlist counts. Navigate
            using Arrow keys, Tab, and Enter like an Excel spreadsheet.
          </p>
        </DialogHeader>

        {/* Set Selector */}
        <div className="flex items-center gap-3 shrink-0 py-2">
          <label htmlFor="set-selector" className="text-sm font-semibold whitespace-nowrap">
            Set:
          </label>
          <select
            id="set-selector"
            value={selectedSet}
            onChange={(e) => setSelectedSet(e.target.value)}
            className="w-80 rounded border border-white/20 bg-[#141416] text-white px-3 py-1.5 text-sm font-medium focus:outline-none focus:ring-1 focus:ring-blue-500"
          >
            {sets.map((s) => {
              const codeDisplay = s.code
                ? ` (${s.code.toUpperCase()})`
                : s.value !== s.label
                  ? ` (${s.value.toUpperCase()})`
                  : "";
              return (
                <option key={s.value} value={s.value} className="bg-[#141416] text-white">
                  {s.label}
                  {codeDisplay}
                </option>
              );
            })}
          </select>

          {isLoading && (
            <span className="text-xs text-muted-foreground animate-pulse">Loading set cards…</span>
          )}
        </div>
        {loadError && (
          <p role="alert" className="text-sm text-red-400">
            Failed to load all printings and collection quantities. Editing is unavailable.
          </p>
        )}

        {/* Spreadsheet Table */}
        <div className="flex-1 overflow-auto rounded border border-white/10 min-h-[300px]">
          <table ref={tableRef} className="w-full border-collapse text-left text-xs">
            <thead className="sticky top-0 z-10 bg-[#1c1c1f] font-semibold text-zinc-300 border-b border-white/10">
              <tr>
                <th className="py-2.5 px-3 w-16 text-center">#</th>
                <th className="py-2.5 px-3">Card Name</th>
                <th className="py-2.5 px-3 w-20 text-center">Rarity</th>
                <th className="py-2.5 px-3 w-24 text-center">Owned</th>
                <th className="py-2.5 px-3 w-24 text-center">Proxy</th>
                <th className="py-2.5 px-3 w-24 text-center">Wishlist</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-border">
              {rows.length === 0 ? (
                <tr>
                  <td colSpan={6} className="py-8 text-center text-muted-foreground">
                    {isLoading ? "Loading cards…" : "No cards found for this set."}
                  </td>
                </tr>
              ) : (
                rows.map((row, index) => {
                  const isModified =
                    row.owned !== row.origOwned ||
                    row.proxy !== row.origProxy ||
                    row.wanted !== row.origWanted;

                  return (
                    <tr
                      key={row.printingId}
                      className={`hover:bg-muted/40 transition-colors ${
                        isModified ? "bg-primary/10" : ""
                      }`}
                    >
                      <td className="py-1.5 px-3 text-center font-mono font-medium text-muted-foreground">
                        #{row.number}
                      </td>
                      <td className="py-1.5 px-3 font-medium">{row.cardName}</td>
                      <td className="py-1.5 px-3 text-center text-muted-foreground">
                        {row.rarity}
                      </td>
                      <td className="py-1 px-2 text-center">
                        <input
                          type="text"
                          inputMode="numeric"
                          data-row={index}
                          data-col={0}
                          value={row.owned}
                          disabled={!isReady || batchMutation.isPending}
                          onChange={(e) => handleCellChange(index, "owned", e.target.value)}
                          onKeyDown={(e) => handleKeyDown(e, index, 0)}
                          onFocus={(e) => e.target.select()}
                          className="h-7 w-16 rounded border border-input bg-background text-center text-xs font-semibold focus:outline-none focus:ring-1 focus:ring-ring"
                        />
                      </td>
                      <td className="py-1 px-2 text-center">
                        <input
                          type="text"
                          inputMode="numeric"
                          data-row={index}
                          data-col={1}
                          value={row.proxy}
                          disabled={!isReady || batchMutation.isPending}
                          onChange={(e) => handleCellChange(index, "proxy", e.target.value)}
                          onKeyDown={(e) => handleKeyDown(e, index, 1)}
                          onFocus={(e) => e.target.select()}
                          className="h-7 w-16 rounded border border-input bg-background text-center text-xs font-semibold focus:outline-none focus:ring-1 focus:ring-ring"
                        />
                      </td>
                      <td className="py-1 px-2 text-center">
                        <input
                          type="text"
                          inputMode="numeric"
                          data-row={index}
                          data-col={2}
                          value={row.wanted}
                          disabled={!isReady || batchMutation.isPending}
                          onChange={(e) => handleCellChange(index, "wanted", e.target.value)}
                          onKeyDown={(e) => handleKeyDown(e, index, 2)}
                          onFocus={(e) => e.target.select()}
                          className="h-7 w-16 rounded border border-input bg-background text-center text-xs font-semibold focus:outline-none focus:ring-1 focus:ring-ring"
                        />
                      </td>
                    </tr>
                  );
                })
              )}
            </tbody>
          </table>
        </div>

        {/* Footer Actions */}
        <div className="flex items-center justify-between pt-3 mt-2 border-t border-border shrink-0">
          <div className="text-xs text-muted-foreground">
            {modifiedRows.length > 0 ? (
              <span className="font-semibold text-primary">
                {modifiedRows.length} card{modifiedRows.length === 1 ? "" : "s"} modified
              </span>
            ) : (
              "No modifications"
            )}
          </div>
          <div className="flex items-center gap-2">
            <button
              type="button"
              onClick={() => onOpenChange(false)}
              className="rounded border border-input bg-background px-4 py-2 text-sm font-medium hover:bg-muted"
            >
              Cancel
            </button>
            <button
              type="button"
              disabled={!isReady || batchMutation.isPending || modifiedRows.length === 0}
              onClick={handleSaveAll}
              className="rounded bg-primary px-4 py-2 text-sm font-semibold text-primary-foreground hover:bg-primary/90 disabled:opacity-50"
            >
              {batchMutation.isPending ? "Saving to Collection…" : "Add to Collection"}
            </button>
          </div>
        </div>
      </DialogContent>
    </Dialog>
  );
}
