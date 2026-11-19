import { useState, type FormEvent } from "react";
import { Dialog, DialogContent, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import type { WishlistItemDto } from "./api";

type MoveToCollectionDialogProps = {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  items: WishlistItemDto[];
  isPending: boolean;
  onConfirm: (
    quantities: Map<number, number>,
    selectedSources: Map<number, number | null>
  ) => Promise<void>;
};

const SOURCE_GENERAL_KEY = "__general__";
const toSourceKey = (deckId: number | null) =>
  deckId == null ? SOURCE_GENERAL_KEY : String(deckId);
const fromSourceKey = (key: string): number | null =>
  key === SOURCE_GENERAL_KEY ? null : Number(key);

/**
 * Controlled dialog for specifying how many copies of one or more wishlist items to move to the collection.
 */
export function MoveToCollectionDialog({
  open,
  onOpenChange,
  items,
  isPending,
  onConfirm,
}: MoveToCollectionDialogProps) {
  const [quantities, setQuantities] = useState<Record<number, string>>({});
  const [sourcesByPrinting, setSourcesByPrinting] = useState<Record<number, string>>({});
  const [error, setError] = useState<string | null>(null);

  const getSelectedSourceKey = (item: WishlistItemDto) => {
    if (sourcesByPrinting[item.cardPrintingId]) return sourcesByPrinting[item.cardPrintingId];
    const first = item.sources?.[0];
    return first ? toSourceKey(first.deckId) : SOURCE_GENERAL_KEY;
  };

  const getMaxForItem = (item: WishlistItemDto) => {
    if (!item.sources || item.sources.length <= 1) return item.quantityWanted;
    const key = getSelectedSourceKey(item);
    const match = item.sources.find((s) => toSourceKey(s.deckId) === key);
    return match ? match.quantity : item.quantityWanted;
  };

  const getValue = (item: WishlistItemDto) =>
    quantities[item.cardPrintingId] ?? String(Math.max(1, getMaxForItem(item)));

  const handleSubmit = async (event: FormEvent) => {
    event.preventDefault();
    setError(null);

    const parsed = new Map<number, number>();
    const selectedSources = new Map<number, number | null>();
    for (const item of items) {
      const raw = getValue(item).trim();
      const num = Number(raw);
      if (!Number.isFinite(num) || Math.trunc(num) <= 0) {
        setError(`Quantity for ${item.cardName} must be a positive number.`);
        return;
      }
      parsed.set(item.cardPrintingId, Math.trunc(num));
      if (item.sources && item.sources.length > 0) {
        selectedSources.set(item.cardPrintingId, fromSourceKey(getSelectedSourceKey(item)));
      }
    }

    await onConfirm(parsed, selectedSources);
  };

  return (
    <Dialog
      open={open}
      onOpenChange={(next) => {
        if (!next) {
          setQuantities({});
          setSourcesByPrinting({});
          setError(null);
        }
        onOpenChange(next);
      }}
    >
      <DialogContent className="max-w-md bg-[#242424] border border-white/20 p-6 text-card-foreground">
        <DialogHeader>
          <DialogTitle>
            {items.length === 1
              ? `Move ${items[0].cardName} to collection`
              : `Move ${items.length} items to collection`}
          </DialogTitle>
        </DialogHeader>
        <form onSubmit={handleSubmit} className="space-y-4">
          <div className="max-h-64 space-y-3 overflow-y-auto pr-1">
            {items.map((item) => {
              const hasMultiSource = (item.sources?.length ?? 0) > 1;
              return (
                <div key={item.cardPrintingId} className="space-y-1.5 text-sm">
                  <label className="block space-y-1">
                    <span className="font-medium">
                      {item.cardName} ({item.set} #{item.number}, want {item.quantityWanted})
                    </span>
                    <Input
                      type="number"
                      min={1}
                      step={1}
                      value={getValue(item)}
                      onChange={(e) =>
                        setQuantities((prev) => ({
                          ...prev,
                          [item.cardPrintingId]: e.target.value,
                        }))
                      }
                      disabled={isPending}
                    />
                  </label>
                  {hasMultiSource && item.sources && (
                    <label className="block space-y-1 text-xs text-zinc-300">
                      <span>Source</span>
                      <select
                        aria-label={`Source for ${item.cardName}`}
                        value={getSelectedSourceKey(item)}
                        onChange={(e) =>
                          setSourcesByPrinting((prev) => ({
                            ...prev,
                            [item.cardPrintingId]: e.target.value,
                          }))
                        }
                        disabled={isPending}
                        className="w-full rounded border border-white/20 bg-[#1a1a1a] px-2 py-1.5 text-xs text-white"
                      >
                        {item.sources.map((src) => (
                          <option key={toSourceKey(src.deckId)} value={toSourceKey(src.deckId)}>
                            {src.deckName} (want {src.quantity})
                          </option>
                        ))}
                      </select>
                    </label>
                  )}
                </div>
              );
            })}
          </div>
          {error && (
            <p role="alert" className="text-xs text-red-400">
              {error}
            </p>
          )}
          <div className="flex justify-end gap-2">
            <Button
              type="button"
              variant="outline"
              onClick={() => onOpenChange(false)}
              disabled={isPending}
            >
              Cancel
            </Button>
            <Button type="submit" disabled={isPending}>
              {isPending ? "Moving…" : "Move to collection"}
            </Button>
          </div>
        </form>
      </DialogContent>
    </Dialog>
  );
}
