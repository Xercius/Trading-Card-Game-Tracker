import { Fragment, useCallback, useEffect, useMemo, useState, type ReactNode } from "react";
import { resolveImageUrl } from "@/lib/http";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import {
  Dialog,
  DialogContent,
  DialogClose,
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
import CardImages from "./CardImages";

type ToastState = {
  type: "success" | "error";
  message: string;
};

type CardModalProps = {
  cardId: number;
  open: boolean;
  onOpenChange: (_open: boolean) => void;
  initialPrintingId?: number;
};

const EMPTY_PRINTINGS: PrintingSummary[] = [];
const BLOCKED_TAGS = new Set(["script", "style", "iframe", "object", "embed"]);

function clampQuantity(value: number): number {
  if (!Number.isFinite(value) || value <= 0) return 1;
  return Math.min(999, Math.floor(value));
}

function domNodeToReact(node: ChildNode, key: string): ReactNode {
  if (node.nodeType === Node.TEXT_NODE) {
    return node.textContent;
  }
  if (node.nodeType !== Node.ELEMENT_NODE) {
    return null;
  }

  const element = node as Element;
  const tag = element.tagName.toLowerCase();
  if (BLOCKED_TAGS.has(tag)) {
    return null;
  }

  if (tag === "br") {
    return <br key={key} />;
  }

  const children = Array.from(element.childNodes).map((child, idx) =>
    domNodeToReact(child, `${key}-${idx}`)
  );

  switch (tag) {
    case "p":
      return (
        <p key={key} className="whitespace-pre-line text-sm leading-relaxed">
          {children}
        </p>
      );
    case "strong":
    case "b":
      return <strong key={key}>{children}</strong>;
    case "i":
    case "em":
      return <i key={key}>{children}</i>;
    case "u":
      return <u key={key}>{children}</u>;
    case "sub":
      return <sub key={key}>{children}</sub>;
    case "sup":
      return <sup key={key}>{children}</sup>;
    case "ul":
      return (
        <ul key={key} className="list-disc pl-5 text-sm leading-relaxed">
          {children}
        </ul>
      );
    case "ol":
      return (
        <ol key={key} className="list-decimal pl-5 text-sm leading-relaxed">
          {children}
        </ol>
      );
    case "li":
      return <li key={key}>{children}</li>;
    case "span":
      return <span key={key}>{children}</span>;
    default:
      return <Fragment key={key}>{children}</Fragment>;
  }
}

/**
 * Safely renders styled card text containing basic HTML formatting tags
 * (`<p>`, `<strong>`, `<i>`, `<em>`, `<br>`, etc.) as React elements without raw `dangerouslySetInnerHTML`.
 */
function renderStyledCardText(content: string | null | undefined, fallbackText?: string): ReactNode {
  const trimmed = content?.trim();
  if (!trimmed) {
    return fallbackText ? (
      <p className="whitespace-pre-line text-sm leading-relaxed">{fallbackText}</p>
    ) : null;
  }

  if (!/<[a-z][\s\S]*>/i.test(trimmed)) {
    return <p className="whitespace-pre-line text-sm leading-relaxed">{trimmed}</p>;
  }

  const doc = new DOMParser().parseFromString(trimmed, "text/html");
  const nodes = Array.from(doc.body.childNodes).map((node, index) =>
    domNodeToReact(node, `styled-${index}`)
  );
  const hasBlockParagraph = Array.from(doc.body.children).some(
    (el) => el.tagName.toLowerCase() === "p"
  );

  if (!hasBlockParagraph) {
    return <p className="whitespace-pre-line text-sm leading-relaxed">{nodes}</p>;
  }

  return <div className="space-y-2">{nodes}</div>;
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

  const printings = printingsQuery.data ?? EMPTY_PRINTINGS;
  const details = detailsQuery.data;

  const primaryType = useMemo(() => {
    if (!details) return "";
    return details.cardType?.trim() || details.cardTypes?.[0]?.trim() || "";
  }, [details]);

  const secondaryType = useMemo(() => {
    if (!details) return null;
    const explicitType2 = details.cardType2?.trim();
    if (explicitType2 && explicitType2.toLowerCase() !== primaryType.toLowerCase()) {
      return explicitType2;
    }
    const extraType = details.cardTypes?.find(
      (t) => t.trim().length > 0 && t.trim().toLowerCase() !== primaryType.toLowerCase()
    );
    return extraType?.trim() ?? null;
  }, [details, primaryType]);

  const isUnitCard = useMemo(() => {
    if (!details) return false;
    const allTypes = [
      primaryType,
      secondaryType ?? "",
      ...(details.cardTypes ?? []),
    ];
    return allTypes.some((type) => type.toLowerCase().includes("unit"));
  }, [details, primaryType, secondaryType]);

  const rulesTextSource = details?.textStyled?.trim() || details?.description?.trim() || null;
  const epicActionSource =
    details?.epicActionStyled?.trim() || details?.epicAction?.trim() || null;
  const deployBoxSource =
    details?.deployBoxStyled?.trim() || details?.deployBox?.trim() || null;

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

  const printingDetail = details?.printings?.find((p) => p.id === selectedPrintingId);

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
      <DialogContent
        labelledBy="card-modal-title"
        className="flex h-[min(90dvh,52rem)] w-[min(96vw,64rem)] max-w-none flex-col overflow-hidden bg-[#242424] text-card-foreground"
      >
        <DialogHeader className="[@media(max-height:500px)]:px-4 [@media(max-height:500px)]:pb-2 [@media(max-height:500px)]:pt-3">
          <DialogTitle id="card-modal-title">{details?.name ?? "Loading card..."}</DialogTitle>
        </DialogHeader>
        <DialogClose aria-label="Close card dialog" />
        <div className="grid min-h-0 flex-1 grid-cols-[minmax(0,0.8fr)_minmax(0,1.2fr)] gap-6 p-6 [@media(max-height:500px)]:gap-3 [@media(max-height:500px)]:p-3">
          <section
            aria-label="Card details and actions"
            className="flex min-h-0 flex-col gap-4 [@media(max-height:500px)]:gap-2"
          >
            <div className="min-h-0 flex-1 space-y-3 overflow-y-auto [@media(max-height:500px)]:space-y-2">
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
                      {primaryType && (
                        <>
                          <span className="font-medium">Type:</span>
                          <span>{primaryType}</span>
                        </>
                      )}
                      {secondaryType && (
                        <>
                          <span className="font-medium">Type 2:</span>
                          <span>{secondaryType}</span>
                        </>
                      )}
                      {isUnitCard && details.arena && (
                        <>
                          <span className="font-medium">Arena:</span>
                          <span>{details.arena}</span>
                        </>
                      )}
                      {details.aspects && details.aspects.length > 0 && (
                        <>
                          <span className="font-medium">Aspects:</span>
                          <span>
                            {details.aspects
                              .map(({ name, count }) => `${name}${count > 1 ? ` ×${count}` : ""}`)
                              .join(", ")}
                          </span>
                        </>
                      )}
                      {details.traits && details.traits.length > 0 && (
                        <>
                          <span className="font-medium">Traits:</span>
                          <span>{details.traits.join(", ")}</span>
                        </>
                      )}
                      {details.cost != null && (
                        <>
                          <span className="font-medium">Cost:</span>
                          <span>{details.cost}</span>
                        </>
                      )}
                      {details.power != null && (
                        <>
                          <span className="font-medium">Power:</span>
                          <span>{details.power}</span>
                        </>
                      )}
                      {details.hp != null && (
                        <>
                          <span className="font-medium">HP:</span>
                          <span>{details.hp}</span>
                        </>
                      )}
                    </div>
                    <div className="space-y-1">
                      <h3 className="font-medium">Rules Text</h3>
                      {renderStyledCardText(rulesTextSource, "No rules text available.")}
                    </div>
                    {epicActionSource && (
                      <div className="space-y-1">
                        <h3 className="font-medium">Epic Action</h3>
                        {renderStyledCardText(epicActionSource)}
                      </div>
                    )}
                    {deployBoxSource && (
                      <div className="space-y-1">
                        <h3 className="font-medium">Deploy Box</h3>
                        {renderStyledCardText(deployBoxSource)}
                      </div>
                    )}
                  </>
                )}
              </div>
              <section
                aria-label="Card printings"
                className="shrink-0 space-y-3 border-t pt-3 [@media(max-height:500px)]:space-y-1 [@media(max-height:500px)]:pt-2"
              >
                <h2 className="text-lg font-semibold [@media(max-height:500px)]:text-sm">
                  Printings
                </h2>
                <div className="space-y-3 [@media(max-height:500px)]:space-y-1" aria-live="polite">
                  {printingsQuery.isLoading && <p>Loading printings…</p>}
                  {printingsQuery.isError && (
                    <p className="text-destructive">Failed to load printings.</p>
                  )}
                  {printings.length > 0 ? (
                    <div
                      role="listbox"
                      tabIndex={0}
                      onKeyDown={handleCarouselKeyDown}
                      className="flex gap-3 overflow-x-auto pb-1 [@media(max-height:500px)]:gap-2"
                      aria-label="Card printing options"
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
                            className={`flex w-24 shrink-0 flex-col items-center gap-2 rounded-xl border p-2 text-xs transition focus:outline-none focus:ring-2 focus:ring-primary [@media(max-height:500px)]:w-16 [@media(max-height:500px)]:gap-1 [@media(max-height:500px)]:rounded-lg [@media(max-height:500px)]:p-1 ${
                              isActive
                                ? "border-primary bg-primary/10"
                                : "border-border hover:border-primary"
                            }`}
                            onClick={() => setSelectedPrintingId(printing.printingId)}
                          >
                            <div className="h-24 w-full rounded-lg bg-muted [@media(max-height:500px)]:h-10">
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
              </section>
            </div>
            <div className="flex shrink-0 flex-col gap-3 border-t pt-4 [@media(max-height:500px)]:gap-2 [@media(max-height:500px)]:pt-2">
              <label className="flex items-center gap-2 text-sm [@media(max-height:500px)]:gap-1 [@media(max-height:500px)]:text-xs">
                Quantity
                <Input
                  type="number"
                  min={1}
                  value={quantity}
                  onChange={(event) =>
                    setQuantity(clampQuantity(Number(event.currentTarget.value)))
                  }
                  className="w-20 [@media(max-height:500px)]:h-8 [@media(max-height:500px)]:w-16"
                />
              </label>
              <div className="flex flex-wrap gap-2 [@media(max-height:500px)]:gap-1">
                <Button
                  onClick={() => onAdd("collection")}
                  disabled={!selectedPrintingId || collectionMutation.isPending}
                  aria-label="Add to collection"
                  className="[@media(max-height:500px)]:h-7 [@media(max-height:500px)]:px-2 [@media(max-height:500px)]:text-xs"
                >
                  {collectionMutation.isPending ? "Adding…" : "Add to Collection"}
                </Button>
                <Button
                  variant="secondary"
                  onClick={() => onAdd("wishlist")}
                  disabled={!selectedPrintingId || wishlistMutation.isPending}
                  aria-label="Add to wishlist"
                  className="[@media(max-height:500px)]:h-7 [@media(max-height:500px)]:px-2 [@media(max-height:500px)]:text-xs"
                >
                  {wishlistMutation.isPending ? "Adding…" : "Add to Wishlist"}
                </Button>
              </div>
            </div>
          </section>

          <section
            aria-label="Selected card image"
            className="flex min-h-0 min-w-0 flex-col items-center justify-center gap-3 [@media(max-height:500px)]:gap-1"
          >
            <div className="min-h-0 w-full flex-1">
              <CardImages
                name={details?.name ?? "Selected printing"}
                imageUrl={selectedPrinting?.imageUrl ?? printingDetail?.imageUrl ?? null}
                backImageUrl={selectedPrinting?.backImageUrl ?? printingDetail?.backImageUrl}
                imageOrientation={
                  selectedPrinting?.imageOrientation ?? printingDetail?.imageOrientation
                }
                backImageOrientation={
                  selectedPrinting?.backImageOrientation ?? printingDetail?.backImageOrientation
                }
              />
            </div>
            <div
              className="shrink-0 text-center text-sm text-muted-foreground [@media(max-height:500px)]:text-xs"
              data-testid="selected-printing-label"
            >
              {selectedPrinting ? (
                <>
                  <span className="font-medium">{selectedPrinting.setName}</span>
                  {selectedPrinting.setCode ? ` (${selectedPrinting.setCode})` : ""}
                  {selectedPrinting.number ? ` • #${selectedPrinting.number}` : ""}
                  {selectedPrinting.rarity ? ` • ${selectedPrinting.rarity}` : ""}
                  {selectedPrinting.releaseDate ? ` • ${selectedPrinting.releaseDate}` : ""}
                  {selectedPrinting.style ? ` • ${selectedPrinting.style}` : ""}
                </>
              ) : (
                "Select a printing"
              )}
            </div>
          </section>
        </div>

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
