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
  onConfirm: (quantities: Map<number, number>) => Promise<void>;
};

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
  const [error, setError] = useState<string | null>(null);

  const getValue = (item: WishlistItemDto) =>
    quantities[item.cardPrintingId] ?? String(Math.max(1, item.quantityWanted));

  const handleSubmit = async (event: FormEvent) => {
    event.preventDefault();
    setError(null);

    const parsed = new Map<number, number>();
    for (const item of items) {
      const raw = getValue(item).trim();
      const num = Number(raw);
      if (!Number.isFinite(num) || Math.trunc(num) <= 0) {
        setError(`Quantity for ${item.cardName} must be a positive number.`);
        return;
      }
      parsed.set(item.cardPrintingId, Math.trunc(num));
    }

    await onConfirm(parsed);
  };

  return (
    <Dialog
      open={open}
      onOpenChange={(next) => {
        if (!next) {
          setQuantities({});
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
            {items.map((item) => (
              <label key={item.cardPrintingId} className="block space-y-1 text-sm">
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
            ))}
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
