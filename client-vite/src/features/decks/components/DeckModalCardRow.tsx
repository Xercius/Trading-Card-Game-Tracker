import { CardImageHoverPreview } from "@/components/ui/CardImageHoverPreview";
import type { DeckCardWithAvailability } from "../api";
import {
  calculateAspectPenalty,
  isDefensiveShield,
  isWithinColorIdentity,
  type DeckZoneKind,
  type GameCapabilities,
} from "../utils/deckRules";
import { PrintingPickerPopover } from "./PrintingPickerPopover";

type Props = {
  card: DeckCardWithAvailability;
  zone: DeckZoneKind;
  capabilities: GameCapabilities;
  includeProxies: boolean;
  deckAspects: Record<string, number>;
  commanderCount: number;
  commanderColorIdentity: string[];
  onToggleStarting: (card: DeckCardWithAvailability) => void;
  onToggleCommander: (card: DeckCardWithAvailability) => void;
  onAdjustSideboard: (card: DeckCardWithAvailability, delta: number) => void;
  onAdjustQuantity: (card: DeckCardWithAvailability, delta: number) => void;
  onAdjustIdea: (card: DeckCardWithAvailability, delta: number) => void;
  onPromoteIdea: (card: DeckCardWithAvailability) => void;
  onMoveToIdea: (card: DeckCardWithAvailability) => void;
  onRemoveCard: (card: DeckCardWithAvailability, zone: DeckZoneKind) => void;
  onSwitchPrinting: (fromPrintingId: number, toPrintingId: number) => void;
};

/**
 * Renders an individual card row inside DeckConstructionModal's right-column zone list.
 */
export function DeckModalCardRow({
  card,
  zone,
  capabilities,
  includeProxies,
  deckAspects,
  commanderCount,
  commanderColorIdentity,
  onToggleStarting,
  onToggleCommander,
  onAdjustSideboard,
  onAdjustQuantity,
  onAdjustIdea,
  onPromoteIdea,
  onMoveToIdea,
  onRemoveCard,
  onSwitchPrinting,
}: Props) {
  const isSideboard = zone === "sideboard";
  const isIdeas = zone === "ideas";
  const penalty = capabilities.isSwu
    ? calculateAspectPenalty(card.aspects, deckAspects, card.cost)
    : null;
  const isShield = capabilities.isSwccg && isDefensiveShield(card);
  const isOutOfIdentity =
    capabilities.hasCommander &&
    zone === "main" &&
    !card.isCommander &&
    commanderCount === 1 &&
    !isWithinColorIdentity(card.colorIdentity, commanderColorIdentity);
  const displayQty = isSideboard
    ? (card.quantitySideboard ?? 0)
    : isIdeas
      ? (card.quantityIdea ?? 0)
      : card.quantityInDeck;

  const handleStep = (delta: number) => {
    if (isSideboard) onAdjustSideboard(card, delta);
    else if (isIdeas) onAdjustIdea(card, delta);
    else onAdjustQuantity(card, delta);
  };

  return (
    <div className="flex items-center justify-between gap-2 p-1.5 rounded-lg bg-zinc-900/70 hover:bg-zinc-800/80 border border-zinc-800/80 transition-colors">
      <div className="flex items-center gap-2 min-w-0">
        <div className="flex items-center justify-center w-12 h-11 shrink-0">
          <CardImageHoverPreview
            imageUrl={card.imageUrl}
            cardName={card.cardName}
            isHorizontal={card.isHorizontal}
          />
        </div>
        <div className="min-w-0">
          <div className="text-xs font-medium text-zinc-200 truncate" title={card.cardName}>
            {card.cardName}
          </div>
          <div className="flex items-center gap-2 text-[10px] text-zinc-500">
            {capabilities.isSwu && penalty && penalty.penaltyCost > 0 && (
              <span className="text-amber-400">
                Cost: {penalty.effectiveCost} (+{penalty.penaltyCost})
              </span>
            )}
            {isOutOfIdentity && <span className="text-red-400 font-semibold">Out of Identity</span>}
            <span>Avail: {includeProxies ? card.availabilityWithProxies : card.availability}</span>
          </div>
        </div>
      </div>

      <div className="flex items-center gap-1.5 shrink-0">
        {capabilities.hasStartingCards && !isShield && zone === "main" && (
          <button
            type="button"
            onClick={() => onToggleStarting(card)}
            className={`px-1.5 py-0.5 rounded text-[10px] font-semibold border transition-all ${
              card.isStarting
                ? "border-amber-400/60 bg-amber-500/20 text-amber-300"
                : "border-zinc-800 bg-zinc-900 text-zinc-500 hover:text-zinc-300 hover:border-zinc-700"
            }`}
            title={card.isStarting ? "Marked as Starting Card" : "Click to mark as Starting Card"}
          >
            {card.isStarting ? "★ Start" : "☆ Start"}
          </button>
        )}
        {capabilities.hasCommander &&
          zone === "main" &&
          (card.canBeCommander || card.isCommander) && (
            <button
              type="button"
              onClick={() => onToggleCommander(card)}
              className={`px-1.5 py-0.5 rounded text-[10px] font-semibold border transition-all ${
                card.isCommander
                  ? "border-amber-400/60 bg-amber-500/20 text-amber-300"
                  : "border-zinc-800 bg-zinc-900 text-zinc-500 hover:text-zinc-300 hover:border-zinc-700"
              }`}
              title={card.isCommander ? "Designated Commander" : "Set as Commander"}
            >
              {card.isCommander ? "★ Cmdr" : "☆ Cmdr"}
            </button>
          )}
        {capabilities.hasSideboard && zone === "main" && (
          <button
            type="button"
            onClick={() => onAdjustSideboard(card, 1)}
            className="px-1.5 py-0.5 rounded text-[10px] font-semibold border border-zinc-800 bg-zinc-900 text-zinc-400 hover:text-sky-300 hover:border-zinc-700 transition-all"
            title="Add 1 copy to Sideboard"
          >
            +SB
          </button>
        )}
        {zone === "main" && (
          <button
            type="button"
            onClick={() => onMoveToIdea(card)}
            className="px-1.5 py-0.5 rounded text-[10px] font-semibold border border-zinc-800 bg-zinc-900 text-purple-300 hover:border-purple-500/50 transition-all"
            title="Add 1 copy to Ideas"
          >
            +Idea
          </button>
        )}
        {isIdeas && (
          <button
            type="button"
            onClick={() => onPromoteIdea(card)}
            className="px-1.5 py-0.5 rounded text-[10px] font-semibold border border-purple-500/50 bg-purple-500/10 text-purple-300 hover:bg-purple-500/20 transition-all"
          >
            Move to Deck
          </button>
        )}
        {card.cardId != null && (
          <PrintingPickerPopover
            cardId={card.cardId}
            currentPrintingId={card.printingId}
            currentLabel={card.setName ?? "Printing"}
            ariaLabel={`Switch printing for ${card.cardName}`}
            onSelectPrinting={(targetId) => {
              if (targetId !== card.printingId) onSwitchPrinting(card.printingId, targetId);
            }}
          />
        )}
        <div className="flex items-center bg-zinc-800 rounded border border-zinc-700">
          <button
            type="button"
            onClick={() => handleStep(-1)}
            aria-label={`Decrease ${card.cardName} quantity`}
            className="w-5 h-5 flex items-center justify-center text-xs text-zinc-300 hover:bg-zinc-700 hover:text-white"
            title="Decrement"
          >
            −
          </button>
          <span className="w-5 text-center text-xs font-bold text-sky-400">{displayQty}</span>
          <button
            type="button"
            disabled={isShield && zone === "main" && card.quantityInDeck >= 1}
            onClick={() => handleStep(1)}
            aria-label={`Increase ${card.cardName} quantity`}
            className="w-5 h-5 flex items-center justify-center text-xs text-zinc-300 hover:bg-zinc-700 hover:text-white disabled:opacity-40 disabled:cursor-not-allowed"
            title={
              isShield && card.quantityInDeck >= 1 ? "Max 1 per Defensive Shield" : "Increment"
            }
          >
            +
          </button>
        </div>
        <button
          type="button"
          onClick={() => onRemoveCard(card, zone)}
          aria-label={`Remove ${card.cardName} from ${isSideboard ? "sideboard" : isIdeas ? "ideas" : "deck"}`}
          className="w-5 h-5 flex items-center justify-center rounded text-zinc-500 hover:text-red-400 hover:bg-red-950/30 transition-colors"
          title={
            isSideboard
              ? "Remove from sideboard"
              : isIdeas
                ? "Remove from ideas"
                : "Remove from deck"
          }
        >
          ×
        </button>
      </div>
    </div>
  );
}
