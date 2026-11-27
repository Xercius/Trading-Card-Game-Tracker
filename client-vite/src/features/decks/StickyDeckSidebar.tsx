import { useMemo, useState } from "react";
import type { ValuePoint } from "@/types/value";
import type { DeckCardWithAvailability } from "./api";
import { DeckSidebarHeader } from "./components/DeckSidebarHeader";
import { DeckSidebarCardRow, type AdjustQuantityMeta } from "./components/DeckSidebarCardRow";
import { extractDragData } from "./dragData";
import {
  getGameCapabilities,
  groupDeckCardsByZone,
  validateMtgDeck,
  validateSwccgDeck,
  validateSwuDeck,
} from "./utils/deckRules";
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

type Props = {
  deckName: string;
  game?: string;
  format?: string | null;
  side?: string | null;
  rows: DeckCardWithAvailability[];
  includeProxies: boolean;
  onIncludeProxiesChange: (next: boolean) => void;
  onAdjustQuantity: (printingId: number, delta: number, meta?: AdjustQuantityMeta) => void;
  onSwitchPrinting?: (fromPrintingId: number, toPrintingId: number) => void;
  onAddMissingToWishlist?: (printingId?: number) => void;
  isAddingMissing?: boolean;
  onAdjustSideboard?: (row: DeckCardWithAvailability, delta: number) => void;
  onToggleCommander?: (row: DeckCardWithAvailability) => void;
  onToggleStarting?: (row: DeckCardWithAvailability) => void;
  onMoveToIdea?: (row: DeckCardWithAvailability) => void;
  onAdjustIdea?: (printingId: number, nextIdeaQty: number) => void;
  onPromoteIdea?: (row: DeckCardWithAvailability) => void;
  onOpenImport?: () => void;
  onOpenExport?: () => void;
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
  game = "",
  format,
  side,
  rows,
  includeProxies,
  onIncludeProxiesChange,
  onAdjustQuantity,
  onSwitchPrinting,
  onAddMissingToWishlist,
  isAddingMissing = false,
  onAdjustSideboard,
  onToggleCommander,
  onToggleStarting,
  onMoveToIdea,
  onAdjustIdea,
  onPromoteIdea,
  onOpenImport,
  onOpenExport,
  isLoading = false,
  valueHistory,
  formatCurrency,
}: Props) {
  const [missingOnly, setMissingOnly] = useState(false);
  const capabilities = useMemo(() => getGameCapabilities(game, format), [game, format]);
  const total = useMemo(() => rows.reduce((sum, row) => sum + row.quantityInDeck, 0), [rows]);
  const sideboardTotal = useMemo(
    () => rows.reduce((sum, row) => sum + (row.quantitySideboard ?? 0), 0),
    [rows]
  );
  const totalMissingCopies = useMemo(
    () => rows.reduce((sum, row) => sum + (row.missingQuantity ?? 0), 0),
    [rows]
  );

  const visibleRows = useMemo(
    () => (missingOnly ? rows.filter((r) => (r.missingQuantity ?? 0) > 0) : rows),
    [rows, missingOnly]
  );

  const zoneGroups = useMemo(
    () => groupDeckCardsByZone(visibleRows, game, format),
    [visibleRows, game, format]
  );

  const validationIssues = useMemo(() => {
    if (!game) return undefined;
    if (capabilities.isSwu) return validateSwuDeck(rows, format ?? "Premier").issues;
    if (capabilities.isSwccg) return validateSwccgDeck(rows, format ?? "Standard", side).issues;
    if (capabilities.isMtg) return validateMtgDeck(rows, format ?? "Constructed").issues;
    return undefined;
  }, [game, format, side, rows, capabilities]);

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
      <DeckSidebarHeader
        deckName={deckName}
        total={total}
        sideboardTotal={sideboardTotal}
        hasSideboard={capabilities.hasSideboard}
        includeProxies={includeProxies}
        onIncludeProxiesChange={onIncludeProxiesChange}
        totalMissingCopies={totalMissingCopies}
        missingOnly={missingOnly}
        onToggleMissingOnly={() => setMissingOnly((prev) => !prev)}
        onAddMissingToWishlist={onAddMissingToWishlist ? () => onAddMissingToWishlist() : undefined}
        isAddingMissing={isAddingMissing}
        onOpenImport={onOpenImport}
        onOpenExport={onOpenExport}
        validationIssues={validationIssues}
        valueHistory={valueHistory}
        formatCurrency={formatCurrency}
      />

      <div
        className="flex-1 overflow-y-auto"
        onDragOver={(e) => {
          e.preventDefault();
          e.dataTransfer.dropEffect = "copy";
        }}
        onDrop={handleDrop}
        data-testid="deck-sidebar-dropzone"
      >
        {isLoading ? (
          <div className="p-4 text-sm text-muted-foreground">Loading deck…</div>
        ) : zoneGroups.length === 0 ? (
          <div className="p-4 text-sm text-muted-foreground">
            Drag cards here to add them to the deck.
          </div>
        ) : (
          zoneGroups.map((group) => {
            const isIdeas = group.zone === "ideas";
            return (
              <div
                key={group.key}
                className={isIdeas ? "border-t p-3" : "border-b last:border-b-0"}
                data-testid={isIdeas ? "deck-ideas-section" : `deck-zone-${group.key}`}
              >
                <div
                  className={
                    isIdeas
                      ? "mb-2 text-xs font-semibold uppercase tracking-wider text-muted-foreground"
                      : "flex items-center justify-between bg-muted/30 px-3 py-1.5 text-xs font-semibold uppercase tracking-wider text-muted-foreground"
                  }
                >
                  <span>{isIdeas ? `${group.label} (${group.count})` : group.label}</span>
                  {!isIdeas && (
                    <span>
                      {group.maxCount != null ? `${group.count} / ${group.maxCount}` : group.count}
                    </span>
                  )}
                </div>
                <ul className={isIdeas ? "space-y-2" : "divide-y"}>
                  {group.cards.map((row) => (
                    <DeckSidebarCardRow
                      key={`${group.key}-${row.printingId}`}
                      row={row}
                      zone={group.zone}
                      capabilities={capabilities}
                      includeProxies={includeProxies}
                      isAddingMissing={isAddingMissing}
                      onDragStart={handleRowDragStart}
                      onAdjustQuantity={onAdjustQuantity}
                      onSwitchPrinting={onSwitchPrinting}
                      onAddMissingToWishlist={onAddMissingToWishlist}
                      onAdjustSideboard={onAdjustSideboard}
                      onToggleCommander={onToggleCommander}
                      onToggleStarting={onToggleStarting}
                      onMoveToIdea={onMoveToIdea}
                      onAdjustIdea={onAdjustIdea}
                      onPromoteIdea={onPromoteIdea}
                    />
                  ))}
                </ul>
              </div>
            );
          })
        )}
      </div>

      <div
        className="border-t bg-muted/40 p-3 text-center text-xs text-muted-foreground"
        onDragOver={(e) => {
          e.preventDefault();
          e.dataTransfer.dropEffect = "move";
        }}
        onDrop={handleDiscardDrop}
        data-testid="deck-sidebar-discard"
      >
        Drag a card here to remove one copy
      </div>
    </aside>
  );
}
