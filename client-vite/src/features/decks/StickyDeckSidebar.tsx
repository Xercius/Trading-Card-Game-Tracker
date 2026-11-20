import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { resolveImageUrl } from "@/lib/http";
import LineSparkline from "@/components/charts/LineSparkline";
import type { ValuePoint } from "@/types/value";
import type { DeckCardWithAvailability } from "./api";
import { PrintingPickerPopover } from "./components/PrintingPickerPopover";
import { extractDragData } from "./dragData";
import {
  AVAILABILITY_DATA,
  AVAILABILITY_PROXY_DATA,
  CARD_IMAGE_DATA,
  CARD_NAME_DATA,
  DRAG_SOURCE_DATA,
  DRAG_SOURCE_GRID,
  DRAG_SOURCE_SIDEBAR,
  PRINTING_ID_DATA,
} from "./constants";
import { useMemo, useState } from "react";

type AdjustQuantityMeta = {
  cardName?: string;
  imageUrl?: string | null;
  availability?: number;
  availabilityWithProxies?: number;
};

type Props = {
  deckName: string;
  rows: DeckCardWithAvailability[];
  includeProxies: boolean;
  onIncludeProxiesChange: (next: boolean) => void;
  onAdjustQuantity: (printingId: number, delta: number, meta?: AdjustQuantityMeta) => void;
  onSwitchPrinting?: (fromPrintingId: number, toPrintingId: number) => void;
  onAddMissingToWishlist?: (printingId?: number) => void;
  isAddingMissing?: boolean;
  onAdjustIdea?: (printingId: number, nextIdeaQty: number) => void;
  onPromoteIdea?: (row: DeckCardWithAvailability) => void;
  isLoading?: boolean;
  valueHistory: {
    points: ValuePoint[];
    isLoading: boolean;
    isError: boolean;
    latestValue: number | null;
  };
  formatCurrency: (value: number | null) => string;
};

export default function StickyDeckSidebar({
  deckName,
  rows,
  includeProxies,
  onIncludeProxiesChange,
  onAdjustQuantity,
  onSwitchPrinting,
  onAddMissingToWishlist,
  isAddingMissing = false,
  onAdjustIdea,
  onPromoteIdea,
  isLoading = false,
  valueHistory,
  formatCurrency,
}: Props) {
  const [missingOnly, setMissingOnly] = useState(false);
  const total = useMemo(() => rows.reduce((sum, row) => sum + row.quantityInDeck, 0), [rows]);
  const totalMissingCopies = useMemo(
    () => rows.reduce((sum, row) => sum + (row.missingQuantity ?? 0), 0),
    [rows]
  );
  const mainRows = useMemo(() => {
    const active = rows.filter(
      (r) => r.quantityInDeck > 0 || (r.quantitySideboard ?? 0) > 0 || (r.quantityIdea ?? 0) === 0
    );
    return missingOnly ? active.filter((r) => (r.missingQuantity ?? 0) > 0) : active;
  }, [rows, missingOnly]);
  const ideaRows = useMemo(() => rows.filter((r) => (r.quantityIdea ?? 0) > 0), [rows]);

  const hasValueHistory = valueHistory.points.length > 0;

  const handleDrop = (event: React.DragEvent<HTMLDivElement>) => {
    event.preventDefault();
    const data = extractDragData(event.dataTransfer);
    if (data.printingId == null || data.source !== DRAG_SOURCE_GRID) return;

    onAdjustQuantity(data.printingId, 1, {
      cardName: data.cardName,
      imageUrl: data.imageUrl,
      availability: data.availability,
      availabilityWithProxies: data.availabilityWithProxies,
    });
  };

  const handleDragOver = (event: React.DragEvent<HTMLDivElement>) => {
    event.preventDefault();
    event.dataTransfer.dropEffect = "copy";
  };

  const handleDiscardDragOver = (event: React.DragEvent<HTMLDivElement>) => {
    event.preventDefault();
    event.dataTransfer.dropEffect = "move";
  };

  const handleDiscardDrop = (event: React.DragEvent<HTMLDivElement>) => {
    event.preventDefault();
    const data = extractDragData(event.dataTransfer);
    if (data.printingId == null || data.source !== DRAG_SOURCE_SIDEBAR) return;

    onAdjustQuantity(data.printingId, -1, {
      cardName: data.cardName,
      imageUrl: data.imageUrl,
      availability: data.availability,
      availabilityWithProxies: data.availabilityWithProxies,
    });
  };

  const handleRowDragStart = (
    event: React.DragEvent<HTMLLIElement>,
    row: DeckCardWithAvailability
  ) => {
    event.dataTransfer.effectAllowed = "move";
    event.dataTransfer.setData(PRINTING_ID_DATA, String(row.printingId));
    event.dataTransfer.setData(CARD_NAME_DATA, row.cardName);
    if (row.imageUrl) event.dataTransfer.setData(CARD_IMAGE_DATA, row.imageUrl);
    event.dataTransfer.setData(AVAILABILITY_DATA, String(row.availability));
    event.dataTransfer.setData(AVAILABILITY_PROXY_DATA, String(row.availabilityWithProxies));
    event.dataTransfer.setData(DRAG_SOURCE_DATA, DRAG_SOURCE_SIDEBAR);
  };

  return (
    <aside
      className="sticky top-0 flex h-[calc(100vh-64px)] w-80 shrink-0 flex-col border-l bg-background"
      data-testid="deck-sidebar"
    >
      <div className="border-b p-4">
        <h2 className="text-lg font-semibold">{deckName || "Deck"}</h2>
        <div className="mt-1 text-sm text-muted-foreground">Total cards: {total}</div>
        <div className="mt-3 flex flex-wrap items-center gap-2">
          <Button
            size="sm"
            variant={includeProxies ? "default" : "outline"}
            onClick={() => onIncludeProxiesChange(!includeProxies)}
            data-testid="toggle-proxies"
          >
            {includeProxies ? "Showing A + Proxies" : "Showing Owned Only"}
          </Button>
          {totalMissingCopies > 0 && (
            <>
              <Button
                size="sm"
                variant={missingOnly ? "default" : "outline"}
                onClick={() => setMissingOnly((prev) => !prev)}
                data-testid="toggle-missing-only"
              >
                Missing Only
              </Button>
              {onAddMissingToWishlist && (
                <Button
                  size="sm"
                  variant="secondary"
                  disabled={isAddingMissing}
                  onClick={() => onAddMissingToWishlist()}
                  data-testid="add-all-missing-wishlist"
                >
                  {isAddingMissing ? "Adding…" : `Add Missing (${totalMissingCopies})`}
                </Button>
              )}
            </>
          )}
        </div>
        <div className="mt-4 rounded border border-border/60 bg-background p-3">
          <div className="flex items-center justify-between text-xs text-muted-foreground">
            <span>Deck value (last 90 days)</span>
            <span>Proxies excluded.</span>
          </div>
          {valueHistory.isLoading && (
            <p className="mt-2 text-xs text-muted-foreground">Loading value history…</p>
          )}
          {valueHistory.isError && (
            <p className="mt-2 text-xs text-destructive">Failed to load value history.</p>
          )}
          {!valueHistory.isLoading && !valueHistory.isError && !hasValueHistory && (
            <p className="mt-2 text-xs text-muted-foreground">No value data.</p>
          )}
          {hasValueHistory && (
            <div className="mt-2 space-y-2">
              <LineSparkline
                points={valueHistory.points}
                ariaLabel="Deck value over time"
                height={72}
                className="h-20"
                stroke="hsl(var(--primary))"
              />
              <p className="text-xs text-muted-foreground">
                Latest: {formatCurrency(valueHistory.latestValue)}
              </p>
            </div>
          )}
        </div>
      </div>

      <div
        className="flex-1 overflow-y-auto"
        onDragOver={handleDragOver}
        onDrop={handleDrop}
        data-testid="deck-sidebar-dropzone"
      >
        {isLoading ? (
          <div className="p-4 text-sm text-muted-foreground">Loading deck…</div>
        ) : rows.length === 0 ? (
          <div className="p-4 text-sm text-muted-foreground">
            Drag cards here to add them to the deck.
          </div>
        ) : (
          <>
            <ul className="divide-y">
              {mainRows.map((row) => {
                const missing = row.missingQuantity ?? 0;
                return (
                  <li
                    key={row.printingId}
                    className="flex items-start gap-3 p-3"
                    draggable
                    onDragStart={(event) => handleRowDragStart(event, row)}
                    data-testid={`deck-row-${row.printingId}`}
                  >
                    <div className="h-16 w-12 rounded border bg-muted">
                      {row.imageUrl ? (
                        <img
                          loading="lazy"
                          decoding="async"
                          src={resolveImageUrl(row.imageUrl)}
                          alt={row.cardName}
                          className="h-full w-full object-contain"
                        />
                      ) : (
                        <div className="flex h-full w-full items-center justify-center text-[10px] text-muted-foreground">
                          No image
                        </div>
                      )}
                    </div>
                    <div className="flex flex-1 flex-col gap-1">
                      <div className="text-sm font-medium leading-tight">{row.cardName}</div>
                      <div className="flex flex-wrap items-center gap-2 text-xs text-muted-foreground">
                        <span className="font-medium">Qty</span>
                        <Badge variant="secondary" data-testid={`deck-row-${row.printingId}-qty`}>
                          {row.quantityInDeck}
                        </Badge>
                        <Badge variant="outline" data-testid={`deck-row-${row.printingId}-badge-a`}>
                          A {row.availability}
                        </Badge>
                        <Badge
                          variant={includeProxies ? "default" : "outline"}
                          data-testid={`deck-row-${row.printingId}-badge-ap`}
                        >
                          A+P {row.availabilityWithProxies}
                        </Badge>
                        {missing > 0 && (
                          <Badge
                            variant="outline"
                            className="border-red-500/60 bg-red-500/10 text-red-400"
                            data-testid={`deck-row-${row.printingId}-missing`}
                          >
                            Missing {missing}
                          </Badge>
                        )}
                        {missing > 0 && onAddMissingToWishlist && (
                          <button
                            type="button"
                            disabled={isAddingMissing}
                            onClick={() => onAddMissingToWishlist(row.printingId)}
                            className="rounded border border-indigo-500/50 bg-indigo-500/10 px-1.5 py-0.5 text-[11px] font-medium text-indigo-300 hover:bg-indigo-500/20 disabled:opacity-40"
                          >
                            + Wishlist
                          </button>
                        )}
                        {onSwitchPrinting && row.cardId != null && (
                          <PrintingPickerPopover
                            cardId={row.cardId}
                            currentPrintingId={row.printingId}
                            currentLabel={row.setName ?? "Printing"}
                            ariaLabel={`Switch printing for ${row.cardName}`}
                            onSelectPrinting={(targetId) => {
                              if (targetId !== row.printingId) {
                                onSwitchPrinting(row.printingId, targetId);
                              }
                            }}
                          />
                        )}
                      </div>
                    </div>
                    <Button
                      size="icon"
                      variant="ghost"
                      data-testid={`deck-row-${row.printingId}-decrement`}
                      onClick={() =>
                        onAdjustQuantity(row.printingId, -1, {
                          cardName: row.cardName,
                          imageUrl: row.imageUrl,
                          availability: row.availability,
                          availabilityWithProxies: row.availabilityWithProxies,
                        })
                      }
                      disabled={row.quantityInDeck <= 0}
                      aria-label={`Remove ${row.cardName}`}
                    >
                      &minus;
                    </Button>
                  </li>
                );
              })}
            </ul>

            {ideaRows.length > 0 && (
              <div className="border-t p-3" data-testid="deck-ideas-section">
                <h3 className="mb-2 text-xs font-semibold uppercase tracking-wider text-muted-foreground">
                  Ideas ({ideaRows.reduce((s, r) => s + (r.quantityIdea ?? 0), 0)})
                </h3>
                <ul className="space-y-2">
                  {ideaRows.map((row) => {
                    const ideaQty = row.quantityIdea ?? 0;
                    return (
                      <li
                        key={`idea-${row.printingId}`}
                        className="flex items-center justify-between gap-2 rounded border border-border/60 px-2.5 py-1.5 text-xs"
                      >
                        <span className="truncate font-medium">
                          {row.cardName} ({ideaQty})
                        </span>
                        <div className="flex items-center gap-1">
                          {onAdjustIdea && (
                            <>
                              <button
                                type="button"
                                aria-label={`Decrease idea ${row.cardName}`}
                                onClick={() => onAdjustIdea(row.printingId, Math.max(0, ideaQty - 1))}
                                className="rounded px-1.5 py-0.5 hover:bg-muted"
                              >
                                −
                              </button>
                              <button
                                type="button"
                                aria-label={`Increase idea ${row.cardName}`}
                                onClick={() => onAdjustIdea(row.printingId, ideaQty + 1)}
                                className="rounded px-1.5 py-0.5 hover:bg-muted"
                              >
                                +
                              </button>
                            </>
                          )}
                          {onPromoteIdea && (
                            <button
                              type="button"
                              onClick={() => onPromoteIdea(row)}
                              className="rounded border border-purple-500/50 bg-purple-500/10 px-1.5 py-0.5 text-[11px] text-purple-300 hover:bg-purple-500/20"
                            >
                              Move to Deck
                            </button>
                          )}
                        </div>
                      </li>
                    );
                  })}
                </ul>
              </div>
            )}
          </>
        )}
      </div>

      <div
        className="border-t bg-muted/40 p-3 text-center text-xs text-muted-foreground"
        onDragOver={handleDiscardDragOver}
        onDrop={handleDiscardDrop}
        data-testid="deck-sidebar-discard"
      >
        Drag a card here to remove one copy
      </div>
    </aside>
  );
}
