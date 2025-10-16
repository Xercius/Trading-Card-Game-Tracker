import { useCallback, useEffect, useMemo, useState } from "react";
import { resolveImageUrl } from "@/lib/http";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import {
  Dialog,
  DialogContent,
  DialogClose,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import {
  useCardDetails,
  useCardPrintings,
  useUpsertCollection,
  useUpsertWishlist,
  type PrintingSummary,
} from "../api";

type ToastState = {
  type: "success" | "error";
  message: string;
};

type CardModalProps = {
  cardId: number;
  open: boolean;
  onOpenChange: (open: boolean) => void;
  initialPrintingId?: number;
};

function clampQuantity(value: number): number {
  if (!Number.isFinite(value) || value <= 0) return 1;
  return Math.min(999, Math.floor(value));
}

export default function CardModal({
  cardId,
  open,
  onOpenChange,
  initialPrintingId,
}: CardModalProps) {
  const detailsQuery = useCardDetails(open ? cardId : 0);
  const printingsQuery = useCardPrintings(open ? cardId : 0);

  const [selectedPrintingId, setSelectedPrintingId] = useState<number | null>(
    initialPrintingId ?? null
  );
  const [quantity, setQuantity] = useState<number>(1);
  const [toast, setToast] = useState<ToastState | null>(null);

  const collectionMutation = useUpsertCollection();
  const wishlistMutation = useUpsertWishlist();

  const printings = printingsQuery.data ?? [];
  const details = detailsQuery.data;

  useEffect(() => {
    if (!open) return;
    setSelectedPrintingId(initialPrintingId ?? null);
  }, [cardId, initialPrintingId, open]);

  useEffect(() => {
    if (!open) return;
    if (selectedPrintingId != null) return;
    const fallback = printings[0]?.printingId ?? details?.printings[0]?.id ?? null;
    if (fallback != null) setSelectedPrintingId(fallback);
  }, [details, open, printings, selectedPrintingId]);

  useEffect(() => {
    if (!open) {
      setQuantity(1);
      setToast(null);
    }
  }, [open]);

  useEffect(() => {
    if (!toast) return;
    const timer = window.setTimeout(() => setToast(null), 2600);
    return () => window.clearTimeout(timer);
  }, [toast]);

  const selectedPrinting: PrintingSummary | null = useMemo(() => {
    if (selectedPrintingId == null) return null;
    return printings.find((p) => p.printingId === selectedPrintingId) ?? null;
  }, [printings, selectedPrintingId]);

  const resolvedImageUrl = useMemo(() => {
    const fallback = details?.printings?.find((p) => p.id === selectedPrintingId)?.imageUrl ?? null;
    const src = selectedPrinting?.imageUrl ?? fallback;
    return src ? resolveImageUrl(src) : null;
  }, [details?.printings, selectedPrinting?.imageUrl, selectedPrintingId]);

  const handleCarouselKeyDown = useCallback(
    (event: React.KeyboardEvent<HTMLDivElement>) => {
      if (printings.length === 0) return;
      if (event.key !== "ArrowLeft" && event.key !== "ArrowRight") return;
      event.preventDefault();
      const currentIndex = printings.findIndex((p) => p.printingId === selectedPrintingId);
      if (currentIndex === -1) {
        setSelectedPrintingId(printings[0].printingId);
        return;
      }
      if (event.key === "ArrowRight") {
        const next = printings[(currentIndex + 1) % printings.length];
        setSelectedPrintingId(next.printingId);
      } else {
        const prev = printings[(currentIndex - 1 + printings.length) % printings.length];
        setSelectedPrintingId(prev.printingId);
      }
    },
    [printings, selectedPrintingId]
  );

  const onAdd = useCallback(
    async (target: "collection" | "wishlist") => {
      if (!selectedPrintingId) return;
      const payload = { printingId: selectedPrintingId, quantity };
      try {
        if (target === "collection") {
          await collectionMutation.mutateAsync(payload);
          setToast({ type: "success", message: "Added to collection." });
        } else {
          await wishlistMutation.mutateAsync(payload);
          setToast({ type: "success", message: "Added to wishlist." });
        }
      } catch (error) {
        const message =
          error instanceof Error && error.message
            ? error.message
            : "Something went wrong. Please try again.";
        setToast({ type: "error", message });
      }
    },
    [collectionMutation, quantity, selectedPrintingId, wishlistMutation]
  );

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent labelledBy="card-modal-title" className="bg-card text-card-foreground">
        <DialogHeader>
          <DialogTitle id="card-modal-title">{details?.name ?? "Loading card..."}</DialogTitle>
        </DialogHeader>
        <DialogClose aria-label="Close card dialog" />
        <div className="grid gap-6 p-6 lg:grid-cols-[1fr,1fr]">
          <section className="space-y-4">
            <div className="flex flex-col items-center gap-3">
              <div className="w-full max-w-sm rounded-xl border bg-muted">
                {resolvedImageUrl ? (
                  <img
                    key={resolvedImageUrl}
                    loading="lazy"
                    decoding="async"
                    src={resolvedImageUrl}
                    alt={details?.name ?? "Selected printing"}
                    className="aspect-[63/88] h-full w-full object-contain"
                  />
                ) : (
                  <div className="flex aspect-[63/88] items-center justify-center text-sm text-muted-foreground">
                    No image available
                  </div>
                )}
              </div>
              <div
                className="text-center text-sm text-muted-foreground"
                data-testid="selected-printing-label"
              >
                {selectedPrinting ? (
                  <>
                    <span className="font-medium">{selectedPrinting.setName}</span>
                    {selectedPrinting.number ? ` • #${selectedPrinting.number}` : ""}
                    {selectedPrinting.rarity ? ` • ${selectedPrinting.rarity}` : ""}
                  </>
                ) : (
                  "Select a printing"
                )}
              </div>
            </div>
          </section>

          <section className="space-y-6">
            <div className="space-y-3">
              <h2 className="text-lg font-semibold">Details</h2>
              <div className="space-y-3 text-sm" aria-live="polite">
                {detailsQuery.isLoading && <p>Loading details…</p>}
                {detailsQuery.isError && (
                  <p className="text-destructive">Failed to load card details.</p>
                )}
                {details && (
                  <>
                    <div className="flex flex-wrap gap-x-4 gap-y-2 text-sm">
                      <span className="font-medium">Game:</span>
                      <span>{details.game}</span>
                      <span className="font-medium">Type:</span>
                      <span>{details.cardType}</span>
                    </div>
                    <p className="whitespace-pre-line text-sm leading-relaxed">
                      {details.description?.trim() || "No rules text available."}
                    </p>
                  </>
                )}
              </div>
            </div>

            <div className="space-y-3">
              <h3 className="text-lg font-semibold">Printings</h3>
              <div className="space-y-3" aria-live="polite">
                {printingsQuery.isLoading && <p>Loading printings…</p>}
                {printingsQuery.isError && (
                  <p className="text-destructive">Failed to load printings.</p>
                )}
                {printings.length > 0 ? (
                  <div
                    role="listbox"
                    tabIndex={0}
                    onKeyDown={handleCarouselKeyDown}
                    className="flex gap-3 overflow-x-auto pb-1"
                    aria-label="Card printings"
                  >
                    {printings.map((printing) => {
                      const isActive = printing.printingId === selectedPrintingId;
                      return (
                        <button
                          key={printing.printingId}
                          type="button"
                          role="option"
                          aria-selected={isActive}
                          aria-label={`${printing.setName} ${printing.number ? `#${printing.number}` : ""}`.trim()}
                          className={`flex w-28 flex-col items-center gap-2 rounded-xl border p-2 text-xs transition focus:outline-none focus:ring-2 focus:ring-primary ${
                            isActive
                              ? "border-primary bg-primary/10"
                              : "border-border hover:border-primary"
                          }`}
                          onClick={() => setSelectedPrintingId(printing.printingId)}
                        >
                          <div className="aspect-[63/88] w-full rounded-lg bg-muted">
                            {printing.imageUrl ? (
                              <img
                                src={resolveImageUrl(printing.imageUrl)}
                                alt={printing.setName}
                                loading="lazy"
                                className="h-full w-full object-contain"
                              />
                            ) : (
                              <span className="flex h-full w-full items-center justify-center text-[10px] text-muted-foreground">
                                No image
                              </span>
                            )}
                          </div>
                          <span className="line-clamp-2 text-center font-medium">
                            {printing.setName}
                          </span>
                          {printing.number && (
                            <span className="text-muted-foreground">#{printing.number}</span>
                          )}
                        </button>
                      );
                    })}
                  </div>
                ) : (
                  <p>No alternate printings found.</p>
                )}
              </div>
            </div>
          </section>
        </div>

        <DialogFooter>
          <div className="flex flex-1 flex-col gap-3 sm:flex-row sm:items-center">
            <label className="flex items-center gap-2 text-sm">
              Quantity
              <Input
                type="number"
                min={1}
                value={quantity}
                onChange={(event) => setQuantity(clampQuantity(Number(event.currentTarget.value)))}
                className="w-20"
              />
            </label>
            <div className="ml-auto flex flex-col gap-2 sm:flex-row">
              <Button
                onClick={() => onAdd("collection")}
                disabled={!selectedPrintingId || collectionMutation.isPending}
                aria-label="Add to collection"
              >
                {collectionMutation.isPending ? "Adding…" : "Add to Collection"}
              </Button>
              <Button
                variant="secondary"
                onClick={() => onAdd("wishlist")}
                disabled={!selectedPrintingId || wishlistMutation.isPending}
                aria-label="Add to wishlist"
              >
                {wishlistMutation.isPending ? "Adding…" : "Add to Wishlist"}
              </Button>
            </div>
          </div>
        </DialogFooter>

        {toast && (
          <div
            role="status"
            className={`pointer-events-none absolute inset-x-0 bottom-4 mx-auto w-fit rounded-full px-4 py-2 text-sm shadow ${
              toast.type === "success"
                ? "bg-emerald-500 text-white"
                : "bg-destructive text-destructive-foreground"
            }`}
          >
            {toast.message}
          </div>
        )}
      </DialogContent>
    </Dialog>
  );
}
