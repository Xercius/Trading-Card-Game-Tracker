import { useState } from "react";
import { Dialog, DialogContent, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { resolveImageUrl } from "@/lib/http";
import { getErrorMessage } from "@/lib/getErrorMessage";
import { useCardPrintings } from "@/features/cards/api";
import { useSetCardQuantitiesMutation, usePrintingQuantitiesQuery } from "./api";
import type { PrintingListItem } from "@/features/printings/api/printings";

type QuantityDraft = {
  printingId: number;
  owned: number;
  proxy: number;
  wishlist: number;
};

type AddCardModalProps = {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  initialPrinting: PrintingListItem | null;
  mode?: "collection" | "wishlist";
  onSuccess?: (message: string) => void;
};

export default function AddCardModal({
  open,
  onOpenChange,
  initialPrinting,
  mode = "collection",
  onSuccess,
}: AddCardModalProps) {
  const [selectedPrintingId, setSelectedPrintingId] = useState<number | null>(null);
  const [draft, setDraft] = useState<QuantityDraft | null>(null);
  const [saveError, setSaveError] = useState<string | null>(null);

  const cardId = initialPrinting ? Number(initialPrinting.cardId) : 0;
  const { data: printings = [] } = useCardPrintings(cardId);

  const initialPrintingId = initialPrinting ? Number(initialPrinting.printingId) : null;
  const activePrintingId =
    selectedPrintingId && printings.some((p) => Number(p.printingId) === selectedPrintingId)
      ? selectedPrintingId
      : initialPrintingId;

  // Load existing quantities if user already has copies of this printing
  const quantitiesQuery = usePrintingQuantitiesQuery(activePrintingId);
  const { data: existingQuantities } = quantitiesQuery;

  const quantitiesReady =
    quantitiesQuery.isSuccess && existingQuantities?.cardPrintingId === activePrintingId;

  const hasSavedQuantities =
    quantitiesReady &&
    (existingQuantities.quantityOwned > 0 ||
      existingQuantities.quantityProxyOwned > 0 ||
      existingQuantities.quantityWanted > 0);

  const baseOwned = hasSavedQuantities
    ? existingQuantities.quantityOwned
    : mode === "collection"
      ? 1
      : 0;
  const baseProxy = hasSavedQuantities ? existingQuantities.quantityProxyOwned : 0;
  const baseWishlist = hasSavedQuantities
    ? existingQuantities.quantityWanted
    : mode === "wishlist"
      ? 1
      : 0;

  const activeDraft = draft && draft.printingId === activePrintingId ? draft : null;
  const ownedQty = activeDraft ? activeDraft.owned : baseOwned;
  const proxyQty = activeDraft ? activeDraft.proxy : baseProxy;
  const wishlistQty = activeDraft ? activeDraft.wishlist : baseWishlist;

  const updateDraft = (patch: Partial<Omit<QuantityDraft, "printingId">>) => {
    if (!activePrintingId) return;
    setSaveError(null);
    setDraft({
      printingId: activePrintingId,
      owned: patch.owned ?? ownedQty,
      proxy: patch.proxy ?? proxyQty,
      wishlist: patch.wishlist ?? wishlistQty,
    });
  };

  const selectPrinting = (nextId: number) => {
    setSelectedPrintingId(nextId);
    setDraft(null);
    setSaveError(null);
  };

  const selectedPrinting = printings.find((p) => Number(p.printingId) === activePrintingId);
  const activePrinting = selectedPrinting
    ? { ...selectedPrinting, cardName: initialPrinting?.cardName ?? "Card" }
    : initialPrinting;

  const currentPrintingIndex = printings.findIndex((p) => p.printingId === activePrintingId);

  const handlePrevPrinting = () => {
    if (printings.length <= 1) return;
    const prevIndex = currentPrintingIndex > 0 ? currentPrintingIndex - 1 : printings.length - 1;
    selectPrinting(printings[prevIndex].printingId);
  };

  const handleNextPrinting = () => {
    if (printings.length <= 1) return;
    const nextIndex = currentPrintingIndex < printings.length - 1 ? currentPrintingIndex + 1 : 0;
    selectPrinting(printings[nextIndex].printingId);
  };

  const mutation = useSetCardQuantitiesMutation();

  const handleSave = async (closeAfter: boolean) => {
    if (!activePrintingId || !quantitiesReady) return;
    setSaveError(null);

    try {
      await mutation.mutateAsync({
        printingId: activePrintingId,
        quantityOwned: Math.max(0, ownedQty),
        quantityProxyOwned: Math.max(0, proxyQty),
        quantityWanted: Math.max(0, wishlistQty),
      });

      const cardName = activePrinting?.cardName ?? "Card";
      onSuccess?.(`Saved ${cardName} to ${mode === "collection" ? "collection" : "wishlist"}`);

      if (closeAfter) {
        onOpenChange(false);
      }
    } catch (err) {
      setSaveError(getErrorMessage(err));
    }
  };

  if (!initialPrinting) return null;

  const cardName = activePrinting?.cardName || initialPrinting.cardName;
  const setCode =
    activePrinting && "setCode" in activePrinting
      ? activePrinting.setCode
      : initialPrinting.setCode;
  const setName = activePrinting?.setName || initialPrinting.setName;
  const number = activePrinting?.number || initialPrinting.number;
  const rarity = activePrinting?.rarity || initialPrinting.rarity;
  const style =
    activePrinting && "style" in activePrinting ? activePrinting.style : initialPrinting.style;
  const imageUrl = activePrinting?.imageUrl || initialPrinting.imageUrl;

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-w-2xl bg-[#242424] border border-white/20 p-6 text-card-foreground">
        <DialogHeader className="mb-2">
          <DialogTitle className="text-lg font-bold">
            {mode === "collection" ? "Add to Collection" : "Add to Wishlist"}
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

            {/* Printing Selector (Moxfield Style) */}
            <div className="rounded-md border border-white/10 bg-[#1c1c1f] p-3 space-y-2">
              <div className="flex items-center justify-between text-xs font-semibold uppercase text-muted-foreground">
                <span>Select Printing</span>
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
                  onChange={(e) => selectPrinting(Number(e.target.value))}
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
                  onChange={(e) =>
                    updateDraft({ owned: Math.max(0, parseInt(e.target.value, 10) || 0) })
                  }
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
                  onChange={(e) =>
                    updateDraft({ proxy: Math.max(0, parseInt(e.target.value, 10) || 0) })
                  }
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
                  onChange={(e) =>
                    updateDraft({ wishlist: Math.max(0, parseInt(e.target.value, 10) || 0) })
                  }
                  className="h-9 w-full rounded border border-white/20 bg-[#141416] text-white px-3 py-1 text-sm font-medium focus:outline-none focus:ring-1 focus:ring-blue-500"
                />
              </div>
            </div>
            {quantitiesQuery.isError && (
              <p className="text-xs text-red-400">
                Failed to load this printing's saved quantities.
              </p>
            )}
            {saveError && (
              <p role="alert" className="text-xs text-red-400">
                Failed to save quantities: {saveError}
              </p>
            )}

            {/* Modal Actions */}
            <div className="mt-4 flex flex-wrap items-center justify-end gap-2 pt-2 border-t border-border">
              <button
                type="button"
                onClick={() => onOpenChange(false)}
                className="rounded border border-input bg-background px-4 py-2 text-sm font-medium hover:bg-muted"
              >
                Close
              </button>
              <button
                type="button"
                disabled={!quantitiesReady || mutation.isPending}
                onClick={() => handleSave(false)}
                className="rounded border border-primary/50 bg-secondary px-4 py-2 text-sm font-medium text-secondary-foreground hover:bg-secondary/80 disabled:opacity-50"
              >
                Save & Add More
              </button>
              <button
                type="button"
                disabled={!quantitiesReady || mutation.isPending}
                onClick={() => handleSave(true)}
                className="rounded bg-primary px-4 py-2 text-sm font-semibold text-primary-foreground hover:bg-primary/90 disabled:opacity-50"
              >
                {mutation.isPending ? "Saving…" : "Save & Close"}
              </button>
            </div>
          </div>
        </div>
      </DialogContent>
    </Dialog>
  );
}
