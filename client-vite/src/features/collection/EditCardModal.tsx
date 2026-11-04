import { useState, useEffect } from "react";
import { Dialog, DialogContent, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { resolveImageUrl } from "@/lib/http";
import { useCardPrintings } from "@/features/cards/api";
import { useSetCardQuantitiesMutation, usePrintingQuantitiesQuery, type CollectionItem } from "./api";

type WishlistOrCollectionItem = {
  cardPrintingId: number;
  cardId: number;
  cardName: string;
  game: string;
  set: string;
  number: string;
  rarity: string;
  style?: string;
  imageUrl?: string | null;
  quantityOwned?: number;
  quantityProxyOwned?: number;
  quantityWanted?: number;
};

type EditCardModalProps = {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  item: WishlistOrCollectionItem | CollectionItem | null;
  mode?: "collection" | "wishlist";
  onSuccess?: (message: string) => void;
};

export default function EditCardModal({
  open,
  onOpenChange,
  item,
  mode = "collection",
  onSuccess,
}: EditCardModalProps) {
  const cardId = item ? Number(item.cardId) : 0;
  const originalPrintingId = item ? Number(item.cardPrintingId) : 0;

  const [selectedPrintingId, setSelectedPrintingId] = useState<number | null>(null);

  const { data: printings = [] } = useCardPrintings(cardId);

  const activePrintingId = selectedPrintingId ?? originalPrintingId;

  // Load latest quantities for active printing
  const quantitiesQuery = usePrintingQuantitiesQuery(activePrintingId);
  const { data: currentQuantities } = quantitiesQuery;
  const originalQuantitiesQuery = usePrintingQuantitiesQuery(originalPrintingId);

  const [ownedQty, setOwnedQty] = useState(0);
  const [proxyQty, setProxyQty] = useState(0);
  const [wishlistQty, setWishlistQty] = useState(0);

  useEffect(() => {
    if (item) {
      setSelectedPrintingId(Number(item.cardPrintingId));
      setOwnedQty(item.quantityOwned ?? 0);
      setProxyQty(item.quantityProxyOwned ?? 0);
      setWishlistQty(item.quantityWanted ?? 0);
    }
  }, [item]);

  useEffect(() => {
    setOwnedQty(0);
    setProxyQty(0);
    setWishlistQty(0);
  }, [activePrintingId]);

  useEffect(() => {
    if (quantitiesQuery.isSuccess && currentQuantities?.cardPrintingId === activePrintingId) {
      setOwnedQty(currentQuantities.quantityOwned ?? 0);
      setProxyQty(currentQuantities.quantityProxyOwned ?? 0);
      setWishlistQty(currentQuantities.quantityWanted ?? 0);
    }
  }, [activePrintingId, currentQuantities, quantitiesQuery.isSuccess]);

  const selectedPrinting = printings.find((p) => Number(p.printingId) === activePrintingId);
  const activePrinting = selectedPrinting
    ? { ...selectedPrinting, cardName: item?.cardName ?? "Card" }
    : item;
  const quantitiesReady =
    quantitiesQuery.isSuccess && currentQuantities?.cardPrintingId === activePrintingId;
  const originalQuantitiesReady =
    originalQuantitiesQuery.isSuccess &&
    originalQuantitiesQuery.data?.cardPrintingId === originalPrintingId;

  const currentPrintingIndex = printings.findIndex((p) => p.printingId === activePrintingId);

  const handlePrevPrinting = () => {
    if (printings.length <= 1) return;
    const prevIndex = currentPrintingIndex > 0 ? currentPrintingIndex - 1 : printings.length - 1;
    setSelectedPrintingId(printings[prevIndex].printingId);
  };

  const handleNextPrinting = () => {
    if (printings.length <= 1) return;
    const nextIndex = currentPrintingIndex < printings.length - 1 ? currentPrintingIndex + 1 : 0;
    setSelectedPrintingId(printings[nextIndex].printingId);
  };

  const mutation = useSetCardQuantitiesMutation();

  const handleSave = async () => {
    if (!activePrintingId || !item || !quantitiesReady) return;

    try {
      await mutation.mutateAsync({
        printingId: activePrintingId,
        quantityOwned: Math.max(0, ownedQty),
        quantityProxyOwned: Math.max(0, proxyQty),
        quantityWanted: Math.max(0, wishlistQty),
      });

      const cardName = activePrinting?.cardName ?? "Card";
      onSuccess?.(`Updated ${cardName}`);
      onOpenChange(false);
    } catch (err) {
      console.error("[EditCardModal] Failed to update card:", err);
    }
  };

  const handleRemove = async () => {
    if (!originalPrintingId || !originalQuantitiesReady) return;
    const quantities = originalQuantitiesQuery.data;
    if (!quantities) return;

    try {
      await mutation.mutateAsync({
        printingId: originalPrintingId,
        quantityOwned: mode === "collection" ? 0 : quantities.quantityOwned,
        quantityProxyOwned: mode === "collection" ? 0 : quantities.quantityProxyOwned,
        quantityWanted: mode === "wishlist" ? 0 : quantities.quantityWanted,
      });
      onSuccess?.("Removed card");
      onOpenChange(false);
    } catch (err) {
      console.error("[EditCardModal] Failed to remove card:", err);
    }
  };

  if (!item) return null;

  const cardName = activePrinting?.cardName || item.cardName;
  const setName = activePrinting && "setName" in activePrinting ? activePrinting.setName : item.set;
  const setCode = activePrinting && "setCode" in activePrinting ? activePrinting.setCode : null;
  const number = activePrinting?.number || item.number;
  const rarity = activePrinting?.rarity || item.rarity;
  const style = activePrinting?.style || item.style;
  const imageUrl = activePrinting?.imageUrl || item.imageUrl;

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-w-2xl bg-[#242424] border border-white/20 p-6 text-card-foreground">
        <DialogHeader className="mb-2">
          <DialogTitle className="text-lg font-bold">
            {mode === "collection" ? "Edit Collection Card" : "Edit Wishlist Card"}
          </DialogTitle>
        </DialogHeader>

        <div className="grid grid-cols-1 md:grid-cols-[200px_1fr] gap-6">
          {/* Card Image Left */}
          <div className="flex flex-col items-center">
            {imageUrl ? (
              <img
                src={resolveImageUrl(imageUrl)}
                alt={cardName}
                className="w-full max-w-[200px] rounded-lg border border-border shadow-md object-contain"
              />
            ) : (
              <div className="h-64 w-44 rounded-lg bg-muted border border-border flex items-center justify-center text-sm text-muted-foreground">
                No Image
              </div>
            )}
          </div>

          {/* Form & Printing Switcher Right */}
          <div className="flex flex-col gap-4">
            <div>
              <div className="text-xl font-bold">{cardName}</div>
              <div className="text-sm text-muted-foreground">
                {setName} {setCode ? `[${setCode.toUpperCase()}]` : ""} · #{number} · {rarity}
                {style && style !== "Standard" ? ` · ${style}` : ""}
              </div>
            </div>

            {/* Printing Selector (allows changing printing) */}
            <div className="rounded-md border border-white/10 bg-[#1c1c1f] p-3 space-y-2">
              <div className="flex items-center justify-between text-xs font-semibold uppercase text-muted-foreground">
                <span>Card Printing</span>
                {printings.length > 1 && (
                  <div className="flex items-center gap-2 text-primary font-normal normal-case">
                    <button
                      type="button"
                      onClick={handlePrevPrinting}
                      className="hover:underline text-xs"
                    >
                      ← Prev
                    </button>
                    <span>/</span>
                    <button
                      type="button"
                      onClick={handleNextPrinting}
                      className="hover:underline text-xs"
                    >
                      Next Printing →
                    </button>
                  </div>
                )}
              </div>

              {printings.length > 0 ? (
                <select
                  value={activePrintingId ?? ""}
                  onChange={(e) => setSelectedPrintingId(Number(e.target.value))}
                  className="w-full rounded border border-white/20 bg-[#141416] text-white px-3 py-2 text-sm focus:outline-none focus:ring-1 focus:ring-blue-500"
                >
                  {printings.map((p) => (
                    <option key={p.printingId} value={p.printingId}>
                      {p.setName} {p.setCode ? `(${p.setCode})` : ""} #{p.number}
                      {p.style && p.style !== "Standard" ? ` [${p.style}]` : ""}
                    </option>
                  ))}
                </select>
              ) : (
                <div className="text-xs text-muted-foreground">
                  {setName} #{number}
                </div>
              )}
            </div>

            {/* Quantities Grid */}
            <div className="grid grid-cols-3 gap-3">
              <div className="flex flex-col gap-1">
                <label className="text-xs font-semibold uppercase text-muted-foreground">
                  Owned
                </label>
                <input
                  type="number"
                  min="0"
                  value={ownedQty}
                  disabled={!quantitiesReady || mutation.isPending}
                  onChange={(e) => setOwnedQty(Math.max(0, parseInt(e.target.value, 10) || 0))}
                  className="h-9 w-full rounded border border-white/20 bg-[#141416] text-white px-3 py-1 text-sm font-medium focus:outline-none focus:ring-1 focus:ring-blue-500"
                />
              </div>

              <div className="flex flex-col gap-1">
                <label className="text-xs font-semibold uppercase text-muted-foreground">
                  Proxy
                </label>
                <input
                  type="number"
                  min="0"
                  value={proxyQty}
                  disabled={!quantitiesReady || mutation.isPending}
                  onChange={(e) => setProxyQty(Math.max(0, parseInt(e.target.value, 10) || 0))}
                  className="h-9 w-full rounded border border-white/20 bg-[#141416] text-white px-3 py-1 text-sm font-medium focus:outline-none focus:ring-1 focus:ring-blue-500"
                />
              </div>

              <div className="flex flex-col gap-1">
                <label className="text-xs font-semibold uppercase text-muted-foreground">
                  Wishlist
                </label>
                <input
                  type="number"
                  min="0"
                  value={wishlistQty}
                  disabled={!quantitiesReady || mutation.isPending}
                  onChange={(e) => setWishlistQty(Math.max(0, parseInt(e.target.value, 10) || 0))}
                  className="h-9 w-full rounded border border-white/20 bg-[#141416] text-white px-3 py-1 text-sm font-medium focus:outline-none focus:ring-1 focus:ring-blue-500"
                />
              </div>
            </div>
            {quantitiesQuery.isError && (
              <p className="text-xs text-red-400">Failed to load this printing's saved quantities.</p>
            )}

            {/* Modal Actions */}
            <div className="mt-4 flex flex-wrap items-center justify-between gap-2 pt-2 border-t border-border">
              <button
                type="button"
                onClick={handleRemove}
                disabled={!originalQuantitiesReady || mutation.isPending}
                className="text-xs text-destructive hover:underline"
              >
                Remove from {mode === "collection" ? "collection" : "wishlist"}
              </button>

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
                  disabled={!quantitiesReady || mutation.isPending}
                  onClick={handleSave}
                  className="rounded bg-primary px-4 py-2 text-sm font-semibold text-primary-foreground hover:bg-primary/90 disabled:opacity-50"
                >
                  {mutation.isPending ? "Saving…" : "Save"}
                </button>
              </div>
            </div>
          </div>
        </div>
      </DialogContent>
    </Dialog>
  );
}
