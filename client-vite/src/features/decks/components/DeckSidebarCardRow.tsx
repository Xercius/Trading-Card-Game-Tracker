import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { resolveImageUrl } from "@/lib/http";
import type { DeckCardWithAvailability } from "../api";
import { isDefensiveShield, type DeckZoneKind, type GameCapabilities } from "../utils/deckRules";
import { PrintingPickerPopover } from "./PrintingPickerPopover";

export type AdjustQuantityMeta = {
  cardName?: string;
  imageUrl?: string | null;
  availability?: number;
  availabilityWithProxies?: number;
};

type Props = {
  row: DeckCardWithAvailability;
  zone: DeckZoneKind;
  capabilities: GameCapabilities;
  includeProxies: boolean;
  isAddingMissing: boolean;
  onDragStart: (event: React.DragEvent<HTMLLIElement>, row: DeckCardWithAvailability) => void;
  onAdjustQuantity: (printingId: number, delta: number, meta?: AdjustQuantityMeta) => void;
  onSwitchPrinting?: (fromPrintingId: number, toPrintingId: number) => void;
  onAddMissingToWishlist?: (printingId?: number) => void;
  onAdjustSideboard?: (row: DeckCardWithAvailability, delta: number) => void;
  onToggleCommander?: (row: DeckCardWithAvailability) => void;
  onToggleStarting?: (row: DeckCardWithAvailability) => void;
  onMoveToIdea?: (row: DeckCardWithAvailability) => void;
  onAdjustIdea?: (printingId: number, nextIdeaQty: number) => void;
  onPromoteIdea?: (row: DeckCardWithAvailability) => void;
};

function CardThumbnail({ imageUrl, cardName }: { imageUrl: string | null; cardName: string }) {
  return (
    <div className="h-16 w-12 shrink-0 rounded border bg-muted">
      {imageUrl ? (
        <img
          loading="lazy"
          decoding="async"
          src={resolveImageUrl(imageUrl)}
          alt={cardName}
          className="h-full w-full object-contain"
        />
      ) : (
        <div className="flex h-full w-full items-center justify-center text-[10px] text-muted-foreground">
          No image
        </div>
      )}
    </div>
  );
}

function RoleButtons({
  row,
  zone,
  capabilities,
  onAdjustSideboard,
  onToggleCommander,
  onToggleStarting,
  onMoveToIdea,
}: Pick<
  Props,
  | "row"
  | "zone"
  | "capabilities"
  | "onAdjustSideboard"
  | "onToggleCommander"
  | "onToggleStarting"
  | "onMoveToIdea"
>) {
  if (zone !== "main") return null;
  const isShield = capabilities.isSwccg && isDefensiveShield(row);
  return (
    <>
      {capabilities.hasStartingCards && !isShield && onToggleStarting && (
        <button
          type="button"
          onClick={() => onToggleStarting(row)}
          aria-pressed={Boolean(row.isStarting)}
          className={`rounded border px-1.5 py-0.5 text-[11px] font-medium ${
            row.isStarting
              ? "border-amber-400/60 bg-amber-500/20 text-amber-300"
              : "border-border text-muted-foreground hover:text-foreground"
          }`}
        >
          {row.isStarting ? "★ Start" : "☆ Start"}
        </button>
      )}
      {capabilities.hasCommander &&
        (row.canBeCommander || row.isCommander) &&
        onToggleCommander && (
          <button
            type="button"
            onClick={() => onToggleCommander(row)}
            aria-pressed={Boolean(row.isCommander)}
            className={`rounded border px-1.5 py-0.5 text-[11px] font-medium ${
              row.isCommander
                ? "border-amber-400/60 bg-amber-500/20 text-amber-300"
                : "border-border text-muted-foreground hover:text-foreground"
            }`}
          >
            {row.isCommander ? "★ Cmdr" : "☆ Cmdr"}
          </button>
        )}
      {capabilities.hasSideboard && onAdjustSideboard && (
        <button
          type="button"
          onClick={() => onAdjustSideboard(row, 1)}
          className="rounded border border-border px-1.5 py-0.5 text-[11px] font-medium text-muted-foreground hover:text-foreground"
          title="Add 1 copy to Sideboard"
        >
          +SB
        </button>
      )}
      {onMoveToIdea && (
        <button
          type="button"
          onClick={() => onMoveToIdea(row)}
          className="rounded border border-purple-500/40 bg-purple-500/10 px-1.5 py-0.5 text-[11px] text-purple-300 hover:bg-purple-500/20"
          title="Add 1 copy to Ideas"
        >
          +Idea
        </button>
      )}
    </>
  );
}

/**
 * Renders a single card row inside a zone section of StickyDeckSidebar.
 */
export function DeckSidebarCardRow({
  row,
  zone,
  capabilities,
  includeProxies,
  isAddingMissing,
  onDragStart,
  onAdjustQuantity,
  onSwitchPrinting,
  onAddMissingToWishlist,
  onAdjustSideboard,
  onToggleCommander,
  onToggleStarting,
  onMoveToIdea,
  onAdjustIdea,
  onPromoteIdea,
}: Props) {
  if (zone === "ideas") {
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
  }

  const isSideboard = zone === "sideboard";
  const displayQty = isSideboard ? (row.quantitySideboard ?? 0) : row.quantityInDeck;
  const missing = row.missingQuantity ?? 0;

  return (
    <li
      className="flex items-start gap-3 p-3"
      draggable={!isSideboard}
      onDragStart={(event) => !isSideboard && onDragStart(event, row)}
      data-testid={isSideboard ? `deck-sb-row-${row.printingId}` : `deck-row-${row.printingId}`}
    >
      <CardThumbnail imageUrl={row.imageUrl} cardName={row.cardName} />
      <div className="flex flex-1 flex-col gap-1">
        <div className="text-sm font-medium leading-tight">{row.cardName}</div>
        <div className="flex flex-wrap items-center gap-2 text-xs text-muted-foreground">
          <span className="font-medium">{isSideboard ? "SB" : "Qty"}</span>
          <Badge
            variant="secondary"
            data-testid={
              isSideboard ? `deck-sb-row-${row.printingId}-qty` : `deck-row-${row.printingId}-qty`
            }
          >
            {displayQty}
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
                if (targetId !== row.printingId) onSwitchPrinting(row.printingId, targetId);
              }}
            />
          )}
          <RoleButtons
            row={row}
            zone={zone}
            capabilities={capabilities}
            onAdjustSideboard={onAdjustSideboard}
            onToggleCommander={onToggleCommander}
            onToggleStarting={onToggleStarting}
            onMoveToIdea={onMoveToIdea}
          />
        </div>
      </div>
      <div className="flex items-center gap-0.5">
        <Button
          size="icon"
          variant="ghost"
          data-testid={
            isSideboard
              ? `deck-sb-row-${row.printingId}-decrement`
              : `deck-row-${row.printingId}-decrement`
          }
          onClick={() =>
            isSideboard && onAdjustSideboard
              ? onAdjustSideboard(row, -1)
              : onAdjustQuantity(row.printingId, -1, {
                  cardName: row.cardName,
                  imageUrl: row.imageUrl,
                  availability: row.availability,
                  availabilityWithProxies: row.availabilityWithProxies,
                })
          }
          disabled={displayQty <= 0}
          aria-label={`Remove ${row.cardName}${isSideboard ? " from sideboard" : ""}`}
        >
          &minus;
        </Button>
        {isSideboard && onAdjustSideboard && (
          <Button
            size="icon"
            variant="ghost"
            onClick={() => onAdjustSideboard(row, 1)}
            aria-label={`Add ${row.cardName} to sideboard`}
          >
            +
          </Button>
        )}
      </div>
    </li>
  );
}
