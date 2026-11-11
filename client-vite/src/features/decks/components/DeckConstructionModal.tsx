import * as React from "react";
import { createPortal } from "react-dom";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import { Input } from "@/components/ui/input";
import { CardImageHoverPreview } from "@/components/ui/CardImageHoverPreview";
import { EditableField } from "@/components/ui/EditableField";
import { CardFiltersDrawer } from "@/features/printings/components/CardFiltersDrawer";
import { CardPagination } from "@/features/printings/components/CardPagination";
import { usePrintingFacets } from "@/features/printings/api/usePrintingFacets";
import { usePrintings } from "@/features/printings/api/usePrintings";
import type { CardFilters } from "@/features/cards/filters/useCardFilters";
import type { PrintingListItem } from "@/features/printings/api/printings";
import { isHorizontalPrinting } from "@/features/printings/utils/cardOrientation";
import {
  useDeckDetails,
  useDeckCardsWithAvailability,
  useDeckQuantityMutation,
  usePatchDeckCardMutation,
  useUpdateDeckMutation,
  type DeckCardWithAvailability,
} from "../api";
import {
  SWU_ASPECT_IMAGES,
  computeDeckAspects,
  calculateAspectPenalty,
  validateSwuDeck,
  validateSwccgDeck,
  isDefensiveShield,
  isVirtualCard,
} from "../utils/deckRules";

export type DeckConstructionModalProps = {
  deckId: number | null;
  isOpen: boolean;
  onClose: () => void;
  game?: string;
};

type FilterArrayKey =
  | "games"
  | "sets"
  | "rarities"
  | "types"
  | "arenas"
  | "aspects"
  | "traits"
  | "keywords"
  | "personas"
  | "subTypes"
  | "destinies"
  | "deploys"
  | "forfeits"
  | "powers"
  | "abilities"
  | "lightSideIcons"
  | "darkSideIcons";

export function DeckConstructionModal({
  deckId,
  isOpen,
  onClose,
  game,
}: DeckConstructionModalProps) {
  const { data: deck, isLoading: isDeckLoading } = useDeckDetails(deckId);
  const currentGame = deck?.game || game || "Star Wars Unlimited";
  const isSwu = currentGame.toLowerCase().includes("unlimited");
  const isSwccg = currentGame.toLowerCase().includes("ccg");

  const [includeProxies, setIncludeProxies] = React.useState(true);
  const { data: deckCardsRaw = [] } = useDeckCardsWithAvailability(deckId, includeProxies);
  const deckCards = React.useMemo(
    () => (Array.isArray(deckCardsRaw) ? deckCardsRaw : []),
    [deckCardsRaw]
  );

  const updateDeckMutation = useUpdateDeckMutation();
  const quantityMutation = useDeckQuantityMutation(deckId, includeProxies);
  const patchCardMutation = usePatchDeckCardMutation(deckId, includeProxies);

  // In-aspect filter toggle for SWU
  const [inAspectOnly, setInAspectOnly] = React.useState(true);

  // Format switching compatibility modal state
  const [switchingFormat, setSwitchingFormat] = React.useState<string | null>(null);
  const [leaderToKeep, setLeaderToKeep] = React.useState<number | null>(null);
  const [virtualCardsToRemove, setVirtualCardsToRemove] = React.useState<
    DeckCardWithAvailability[]
  >([]);

  // Quantity mutation error banner
  const [quantityError, setQuantityError] = React.useState<string | null>(null);

  React.useEffect(() => {
    if (quantityMutation.error) {
      const err = quantityMutation.error as { title?: string; message?: string };
      const detail = err.title || err.message || "A server error occurred.";
      setQuantityError(`Unable to update the deck. ${detail}`);
    }
  }, [quantityMutation.error]);

  // Validation details popover state
  const [showValidationDetails, setShowValidationDetails] = React.useState(false);

  // Compute deck aspects from Leader and Base
  const deckAspects = React.useMemo(() => {
    if (!isSwu) return {};
    return computeDeckAspects(deckCards);
  }, [deckCards, isSwu]);

  // Leaders and Base cards
  const leadersInDeck = React.useMemo(() => {
    return deckCards.filter((c) => c.quantityInDeck > 0 && c.cardType?.toLowerCase() === "leader");
  }, [deckCards]);

  const objectiveInDeck = React.useMemo(() => {
    return deckCards.find((c) => c.quantityInDeck > 0 && c.cardType?.toLowerCase() === "objective");
  }, [deckCards]);

  // Current deck format
  const currentFormat = deck?.format || (isSwu ? "Premier" : "Standard");
  const currentSide = deck?.side || (isSwccg ? "Light" : null);
  const requiredLeaders = currentFormat === "Twin Suns" ? 2 : 1;
  const hasLeadersAndBase =
    leadersInDeck.length >= requiredLeaders &&
    deckCards.some((card) => card.quantityInDeck > 0 && card.cardType?.toLowerCase() === "base");
  const aspectFilterActive = isSwu && inAspectOnly && hasLeadersAndBase;
  const availableAspects = React.useMemo(
    () =>
      aspectFilterActive
        ? Object.entries(deckAspects)
            .sort(([a], [b]) => a.localeCompare(b))
            .flatMap(([name, count]) => Array<string>(count).fill(name))
        : undefined,
    [aspectFilterActive, deckAspects]
  );

  // Validation results
  const swuValidation = React.useMemo(() => {
    if (!isSwu) return null;
    return validateSwuDeck(deckCards, currentFormat);
  }, [deckCards, currentFormat, isSwu]);

  const swccgValidation = React.useMemo(() => {
    if (!isSwccg) return null;
    return validateSwccgDeck(deckCards, currentFormat, currentSide);
  }, [deckCards, currentFormat, currentSide, isSwccg]);

  const isDeckLegal = isSwu
    ? Boolean(swuValidation?.isValid)
    : isSwccg
      ? Boolean(swccgValidation?.isValid)
      : true;

  // Search and filter state for card pool
  const [drawerOpen, setDrawerOpen] = React.useState(false);
  const [filters, setFilters] = React.useState<CardFilters>(() => ({
    q: "",
    games: [currentGame],
    sets: [],
    rarities: [],
    types: [],
    arenas: [],
    aspects: [],
    aspectMethod: "or",
    traits: [],
    traitMethod: "or",
    keywords: [],
    keywordMethod: "or",
    side: currentSide || undefined,
    page: 1,
    pageSize: 20,
  }));

  // Update game/side in filter when active deck changes
  React.useEffect(() => {
    setFilters((prev) => ({
      ...prev,
      games: [currentGame],
      side: currentSide || undefined,
      page: 1,
    }));
  }, [currentGame, currentSide]);

  const {
    data: facets,
    isLoading: isFacetsLoading,
    isError: isFacetsError,
    refetch: refetchFacets,
  } = usePrintingFacets(filters);

  const aspectFilterKey = JSON.stringify([deckId, availableAspects]);
  const [previousAspectFilterKey, setPreviousAspectFilterKey] = React.useState(aspectFilterKey);
  if (previousAspectFilterKey !== aspectFilterKey) {
    setPreviousAspectFilterKey(aspectFilterKey);
    setFilters((prev) => ({ ...prev, page: 1 }));
  }

  const { data: printingsData, isLoading: isPrintingsLoading } = usePrintings(
    filters,
    availableAspects
  );

  // Compute deck lookup for fast quantity check in card results
  const deckCardMap = React.useMemo(() => {
    const map = new Map<number, DeckCardWithAvailability>();
    for (const card of deckCards) {
      map.set(card.printingId, card);
    }
    return map;
  }, [deckCards]);

  // SWCCG distinct counts: Defensive Shields vs Main Deck
  const { mainDeckCount, defensiveShieldCount } = React.useMemo(() => {
    if (isSwccg) {
      const shields = deckCards
        .filter((c) => isDefensiveShield(c))
        .reduce((sum, c) => sum + c.quantityInDeck, 0);
      const main = deckCards
        .filter((c) => !isDefensiveShield(c))
        .reduce((sum, c) => sum + c.quantityInDeck, 0);
      return { mainDeckCount: main, defensiveShieldCount: shields };
    }
    const total = deckCards.reduce((acc, c) => acc + c.quantityInDeck, 0);
    return { mainDeckCount: total, defensiveShieldCount: 0 };
  }, [deckCards, isSwccg]);

  // Group deck cards for right sidebar
  const groupedDeckCards = React.useMemo(() => {
    if (isSwccg) {
      const starting = deckCards.filter((c) => c.quantityInDeck > 0 && c.isStarting);
      const shields = deckCards.filter((c) => c.quantityInDeck > 0 && isDefensiveShield(c));
      const otherCards = deckCards.filter(
        (c) => c.quantityInDeck > 0 && !c.isStarting && !isDefensiveShield(c)
      );

      const groups: Record<string, DeckCardWithAvailability[]> = {};
      if (starting.length > 0) groups["⭐ Starting Cards"] = starting;
      if (shields.length > 0) groups["🛡️ Defensive Shields"] = shields;

      for (const card of otherCards) {
        const type = card.cardType || "Other";
        if (!groups[type]) groups[type] = [];
        groups[type].push(card);
      }
      return groups;
    }

    // SWU: Leaders, Base, then other categories
    const leaders = deckCards.filter(
      (c) => c.quantityInDeck > 0 && c.cardType?.toLowerCase() === "leader"
    );
    const bases = deckCards.filter(
      (c) => c.quantityInDeck > 0 && c.cardType?.toLowerCase() === "base"
    );
    const others = deckCards.filter(
      (c) =>
        c.quantityInDeck > 0 &&
        c.cardType?.toLowerCase() !== "leader" &&
        c.cardType?.toLowerCase() !== "base"
    );

    const groups: Record<string, DeckCardWithAvailability[]> = {};
    if (leaders.length > 0) groups["Leaders"] = leaders;
    if (bases.length > 0) groups["Base"] = bases;

    for (const card of others) {
      const type = card.cardType || "Units & Events";
      if (!groups[type]) groups[type] = [];
      groups[type].push(card);
    }
    return groups;
  }, [deckCards, isSwccg]);

  const handleUpdateName = (nextName: string) => {
    if (!deckId || !nextName.trim()) return;
    updateDeckMutation.mutate({ deckId, payload: { name: nextName.trim() } });
  };

  const handleUpdateDescription = (nextDesc: string) => {
    if (!deckId) return;
    updateDeckMutation.mutate({
      deckId,
      payload: { description: nextDesc.trim() || null },
    });
  };

  // Format switching with compatibility check
  const handleInitiateFormatChange = (newFormat: string) => {
    if (!deckId || newFormat === currentFormat) return;

    if (isSwu && currentFormat === "Twin Suns" && newFormat === "Premier") {
      // Check if 2 leaders exist
      if (leadersInDeck.length > 1) {
        setSwitchingFormat("Premier");
        setLeaderToKeep(leadersInDeck[0].printingId);
        return;
      }
    }

    if (isSwccg && newFormat === "Decipher Only") {
      // Check if Virtual cards exist
      const virtuals = deckCards.filter((c) => c.quantityInDeck > 0 && isVirtualCard(c));
      if (virtuals.length > 0) {
        setSwitchingFormat("Decipher Only");
        setVirtualCardsToRemove(virtuals);
        return;
      }
    }

    // Direct change if fully compatible
    updateDeckMutation.mutate({ deckId, payload: { format: newFormat } });
  };

  const handleConfirmLeaderRemoval = async () => {
    if (!deckId || !leaderToKeep || !switchingFormat) return;
    const leaderToRemove = leadersInDeck.find((l) => l.printingId !== leaderToKeep);
    if (leaderToRemove) {
      await quantityMutation.mutateAsync({
        printingId: leaderToRemove.printingId,
        qtyDelta: -leaderToRemove.quantityInDeck,
        cardName: leaderToRemove.cardName,
      });
    }
    updateDeckMutation.mutate({ deckId, payload: { format: switchingFormat } });
    setSwitchingFormat(null);
    setLeaderToKeep(null);
  };

  const handleConfirmVirtualRemoval = async () => {
    if (!deckId || !switchingFormat) return;
    for (const vc of virtualCardsToRemove) {
      await quantityMutation.mutateAsync({
        printingId: vc.printingId,
        qtyDelta: -vc.quantityInDeck,
        cardName: vc.cardName,
      });
    }
    updateDeckMutation.mutate({ deckId, payload: { format: switchingFormat } });
    setSwitchingFormat(null);
    setVirtualCardsToRemove([]);
  };

  const handleAddCard = (card: PrintingListItem) => {
    setQuantityError(null);
    const printingId =
      typeof card.printingId === "number" ? card.printingId : Number(card.printingId);

    // Enforce defensive shield cap (max 1 copy)
    if (isSwccg && isDefensiveShield(card)) {
      const existing = deckCardMap.get(printingId);
      if (existing && existing.quantityInDeck >= 1) return;
    }

    quantityMutation.mutate({
      printingId,
      qtyDelta: 1,
      cardName: card.cardName,
      imageUrl: card.imageUrl,
    });
  };

  const handleAdjustQuantity = (card: DeckCardWithAvailability, delta: number) => {
    setQuantityError(null);
    if (delta > 0 && isSwccg && isDefensiveShield(card) && card.quantityInDeck >= 1) {
      return; // Capped at 1 copy per shield
    }

    quantityMutation.mutate({
      printingId: card.printingId,
      qtyDelta: delta,
      cardName: card.cardName,
      imageUrl: card.imageUrl,
    });
  };

  const handleRemoveCard = (card: DeckCardWithAvailability) => {
    setQuantityError(null);
    quantityMutation.mutate({
      printingId: card.printingId,
      qtyDelta: -card.quantityInDeck,
      cardName: card.cardName,
      imageUrl: card.imageUrl,
    });
  };

  const handleToggleStartingCard = (card: DeckCardWithAvailability) => {
    if (!deckId) return;
    patchCardMutation.mutate({
      printingId: card.printingId,
      updates: { isStarting: !card.isStarting },
    });
  };

  // Filter drawer active filters count and removal
  const activeFilters = React.useMemo(() => {
    const list: Array<{
      key: FilterArrayKey;
      label: string;
      items: Array<{ display: string; value: string }>;
    }> = [];
    if (filters.side) {
      list.push({
        key: "side" as unknown as FilterArrayKey,
        label: "Side",
        items: [{ display: filters.side, value: filters.side }],
      });
    }
    if (filters.types?.length) {
      list.push({
        key: "types",
        label: "Type",
        items: filters.types.map((v) => ({ display: v, value: v })),
      });
    }
    if (filters.sets?.length) {
      list.push({
        key: "sets",
        label: "Set",
        items: filters.sets.map((v) => ({ display: v, value: v })),
      });
    }
    if (filters.rarities?.length) {
      list.push({
        key: "rarities",
        label: "Rarity",
        items: filters.rarities.map((v) => ({ display: v, value: v })),
      });
    }
    return list;
  }, [filters]);

  const activeFilterCount = activeFilters.reduce((sum, f) => sum + f.items.length, 0);

  const removeFilterValue = (key: FilterArrayKey, value: string) => {
    setFilters((prev) => {
      if (key === ("side" as unknown as FilterArrayKey)) {
        return { ...prev, side: undefined, page: 1 };
      }
      const existing = (prev[key] as string[]) || [];
      return {
        ...prev,
        [key]: existing.filter((item) => item !== value),
        page: 1,
      };
    });
  };

  const clearFilters = () => {
    setFilters((prev) => ({
      ...prev,
      sets: [],
      rarities: [],
      types: [],
      arenas: [],
      aspects: [],
      traits: [],
      keywords: [],
      side: currentSide || undefined,
      page: 1,
    }));
  };

  const filteredPrintings = React.useMemo(() => {
    const rawPrintings = Array.isArray(printingsData?.items) ? printingsData.items : [];
    return rawPrintings.filter((card) => {
      if (isSwccg && currentFormat === "Decipher Only") {
        if (isVirtualCard(card)) {
          return false;
        }
      }
      return true;
    });
  }, [printingsData?.items, isSwccg, currentFormat]);

  const totalPrintings = printingsData?.total ?? 0;
  const totalPages = Math.max(1, Math.ceil(totalPrintings / filters.pageSize));

  if (!isOpen) return null;

  return createPortal(
    <div
      role="dialog"
      aria-modal="true"
      aria-labelledby="deck-construction-title"
      className="fixed inset-0 z-50 flex items-center justify-center p-3 sm:p-6 bg-black/80 backdrop-blur-sm animate-in fade-in"
    >
      <div
        className="relative flex flex-col w-full max-w-7xl h-[94vh] rounded-xl border border-zinc-800 bg-zinc-950 text-zinc-100 shadow-2xl overflow-hidden"
        onClick={(e) => e.stopPropagation()}
      >
        {/* Modal Header */}
        <div className="flex flex-col sm:flex-row items-start sm:items-center justify-between border-b border-zinc-800 px-6 py-4 bg-zinc-900/70 gap-4">
          <div className="flex-1 min-w-0 pr-4">
            <div className="flex flex-wrap items-center gap-3">
              <h2 id="deck-construction-title" className="sr-only">
                {deck?.name || "Deck Construction"}
              </h2>
              <EditableField
                value={deck?.name ?? (isDeckLoading ? "Loading..." : "New Deck")}
                onSave={handleUpdateName}
                ariaLabel="Deck Name"
                placeholder="Deck Name"
                textClassName="text-xl sm:text-2xl font-bold tracking-tight text-white hover:text-sky-300"
                inputClassName="text-xl font-bold"
                disabled={isDeckLoading}
              />

              {/* Game Badge */}
              <Badge
                variant="outline"
                className="border-zinc-700 bg-zinc-800 text-zinc-300 text-xs font-normal"
              >
                {currentGame}
              </Badge>

              {/* Format Selector / Badge */}
              <div className="relative inline-flex items-center gap-1.5">
                <select
                  aria-label="Deck Format"
                  value={currentFormat}
                  onChange={(e) => handleInitiateFormatChange(e.target.value)}
                  className="rounded-md border border-zinc-700 bg-zinc-900 px-2.5 py-1 text-xs font-semibold text-zinc-200 focus:outline-none focus:ring-1 focus:ring-sky-500 cursor-pointer"
                >
                  {isSwu ? (
                    <>
                      <option value="Premier">Premier (50+ cards)</option>
                      <option value="Twin Suns">Twin Suns (80+ cards, Singleton)</option>
                    </>
                  ) : isSwccg ? (
                    <>
                      <option value="Standard">Standard (All Sets)</option>
                      <option value="Decipher Only">Decipher Only (No Virtual)</option>
                    </>
                  ) : null}
                </select>

                {isSwccg && currentSide && (
                  <Badge
                    variant="outline"
                    className={`text-xs ${
                      currentSide.toLowerCase() === "light"
                        ? "border-amber-400/40 text-amber-300 bg-amber-950/30"
                        : "border-red-500/40 text-red-300 bg-red-950/30"
                    }`}
                  >
                    {currentSide} Side
                  </Badge>
                )}
              </div>

              {/* Validity Status Pill */}
              <button
                type="button"
                onClick={() => setShowValidationDetails(!showValidationDetails)}
                className={`inline-flex items-center gap-1.5 px-2.5 py-1 rounded-full text-xs font-semibold border transition-all ${
                  isDeckLegal
                    ? "border-emerald-500/50 bg-emerald-950/40 text-emerald-300 hover:bg-emerald-950/60"
                    : "border-amber-500/50 bg-amber-950/40 text-amber-300 hover:bg-amber-950/60"
                }`}
                title="Click to view deck rule checklist"
              >
                <span>{isDeckLegal ? "✓ Legal Deck" : "⚠️ Incomplete"}</span>
              </button>
            </div>

            {/* Description */}
            <div className="mt-1">
              <EditableField
                value={deck?.description ?? ""}
                onSave={handleUpdateDescription}
                multiline
                ariaLabel="Deck Description"
                placeholder="Add a description for this deck..."
                emptyPlaceholder="Click to add deck description..."
                textClassName="text-sm text-zinc-400 hover:text-zinc-200"
                disabled={isDeckLoading}
              />
            </div>
          </div>

          {/* SWU Header Badges: Aspect Icons */}
          {isSwu && (
            <div className="flex items-center gap-4 shrink-0 bg-zinc-900/60 border border-zinc-800 p-2 rounded-lg">
              {/* Inherited Aspect Emblems */}
              <div className="flex flex-col items-center">
                <span className="text-[10px] font-semibold uppercase tracking-wider text-zinc-400 mb-1">
                  Aspects
                </span>
                <div className="flex items-center gap-1.5 min-h-[26px]">
                  {Object.keys(deckAspects).length === 0 ? (
                    <span className="text-xs text-zinc-600 italic">None yet</span>
                  ) : (
                    Object.entries(deckAspects).map(([name, count]) => (
                      <div
                        key={name}
                        className="relative flex items-center"
                        title={`${name} (x${count})`}
                      >
                        <img
                          src={SWU_ASPECT_IMAGES[name]}
                          alt={name}
                          className="w-6 h-6 object-contain"
                        />
                        {count > 1 && (
                          <span className="absolute -bottom-1 -right-1 text-[9px] font-bold bg-zinc-950 text-white px-1 rounded-full border border-zinc-700">
                            {count}
                          </span>
                        )}
                      </div>
                    ))
                  )}
                </div>
              </div>
            </div>
          )}

          {/* SWCCG Header Badges: Objective Preview */}
          {isSwccg && objectiveInDeck && (
            <div className="flex items-center gap-3 shrink-0 bg-zinc-900/60 border border-zinc-800 p-2 rounded-lg">
              <CardImageHoverPreview
                imageUrl={objectiveInDeck.imageUrl}
                cardName={objectiveInDeck.cardName}
                isHorizontal={objectiveInDeck.isHorizontal}
                thumbnailWidth={32}
                thumbnailHeight={44}
              />
              <div className="text-left">
                <div className="text-[10px] font-semibold uppercase tracking-wider text-sky-400">
                  Objective
                </div>
                <div
                  className="text-xs font-medium text-zinc-200 max-w-[150px] truncate"
                  title={objectiveInDeck.cardName}
                >
                  {objectiveInDeck.cardName}
                </div>
              </div>
            </div>
          )}

          <button
            type="button"
            onClick={onClose}
            aria-label="Close modal"
            className="rounded-lg p-2 text-zinc-400 hover:text-white hover:bg-zinc-800 transition-colors focus:outline-none focus:ring-2 focus:ring-sky-500"
          >
            <svg className="w-5 h-5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
              <path
                strokeLinecap="round"
                strokeLinejoin="round"
                strokeWidth={2}
                d="M6 18L18 6M6 6l12 12"
              />
            </svg>
          </button>
        </div>

        {/* Error Alert Banner */}
        {quantityError && (
          <div
            role="alert"
            className="flex items-center justify-between gap-3 px-6 py-2.5 bg-red-950/80 border-b border-red-800 text-red-200 text-xs shrink-0"
          >
            <span>{quantityError}</span>
            <button
              type="button"
              aria-label="Dismiss deck update error"
              onClick={() => setQuantityError(null)}
              className="text-red-400 hover:text-white font-bold p-1 focus:outline-none"
            >
              ✕
            </button>
          </div>
        )}

        {/* Validation Details Flyout / Dropdown */}
        {showValidationDetails && (
          <div className="border-b border-zinc-800 bg-zinc-900/90 px-6 py-3 flex flex-wrap items-center justify-between gap-3 text-xs">
            <div className="flex flex-wrap items-center gap-4">
              <span className="font-bold text-zinc-200">{currentFormat} Deck Requirements:</span>
              {isSwu && swuValidation && (
                <>
                  <span
                    className={
                      swuValidation.leaderCount === swuValidation.requiredLeaderCount
                        ? "text-emerald-400"
                        : "text-amber-400"
                    }
                  >
                    • Leaders: {swuValidation.leaderCount} / {swuValidation.requiredLeaderCount}
                  </span>
                  <span
                    className={
                      swuValidation.baseCount === swuValidation.requiredBaseCount
                        ? "text-emerald-400"
                        : "text-amber-400"
                    }
                  >
                    • Base: {swuValidation.baseCount} / {swuValidation.requiredBaseCount}
                  </span>
                  <span
                    className={
                      swuValidation.otherCount >= swuValidation.minOtherCount
                        ? "text-emerald-400"
                        : "text-amber-400"
                    }
                  >
                    • Deck: {swuValidation.otherCount} / {swuValidation.minOtherCount} min
                  </span>
                  <span
                    className={
                      swuValidation.copyViolations.length === 0
                        ? "text-emerald-400"
                        : "text-amber-400"
                    }
                  >
                    • Max Copies:{" "}
                    {swuValidation.copyViolations.length === 0
                      ? `Valid (max ${swuValidation.maxCopiesAllowed})`
                      : `${swuValidation.copyViolations.length} violations`}
                  </span>
                </>
              )}
              {isSwccg && swccgValidation && (
                <>
                  <span
                    className={
                      swccgValidation.mainDeckCount >= 60 ? "text-emerald-400" : "text-amber-400"
                    }
                  >
                    • Cards: {swccgValidation.mainDeckCount} / 60 min (shields excluded)
                  </span>
                  <span
                    className={
                      swccgValidation.sideViolations.length === 0
                        ? "text-emerald-400"
                        : "text-red-400"
                    }
                  >
                    • Side:{" "}
                    {swccgValidation.sideViolations.length === 0
                      ? `100% ${currentSide}`
                      : "Side mismatch"}
                  </span>
                  {currentFormat === "Decipher Only" && (
                    <span
                      className={
                        swccgValidation.virtualViolations.length === 0
                          ? "text-emerald-400"
                          : "text-red-400"
                      }
                    >
                      • Virtual Sets:{" "}
                      {swccgValidation.virtualViolations.length === 0
                        ? "0 (Valid)"
                        : `${swccgValidation.virtualViolations.length} Virtual cards`}
                    </span>
                  )}
                  <span className="text-zinc-400">
                    • Defensive Shields: {swccgValidation.defensiveShieldCount} (max 1 per shield)
                  </span>
                </>
              )}
            </div>
            <button
              type="button"
              onClick={() => setShowValidationDetails(false)}
              className="text-zinc-500 hover:text-zinc-300"
            >
              Dismiss
            </button>
          </div>
        )}

        {/* Content Body: Left = Search & Pool, Right = Deck Cards */}
        <div className="flex-1 grid grid-cols-1 lg:grid-cols-12 min-h-0 overflow-hidden divide-y lg:divide-y-0 lg:divide-x divide-zinc-800">
          {/* Card Pool & Search Area (Majority) */}
          <div className="lg:col-span-8 flex flex-col min-h-0 bg-zinc-950/50 p-4 space-y-3">
            {/* Search Bar & Controls */}
            <div className="flex flex-wrap items-center gap-2">
              <div className="relative flex-1 min-w-[200px]">
                <Input
                  type="search"
                  value={filters.q}
                  placeholder={`Search ${currentGame} cards by name or text...`}
                  onChange={(e) => setFilters((prev) => ({ ...prev, q: e.target.value, page: 1 }))}
                  className="bg-zinc-900 border-zinc-700 text-sm placeholder:text-zinc-500 focus-visible:ring-sky-500"
                />
              </div>

              {/* SWU In-Aspect filter checkbox */}
              {isSwu && (
                <label
                  title={
                    hasLeadersAndBase
                      ? undefined
                      : "Select your leader(s) and base to enable aspect filtering."
                  }
                  className="flex items-center gap-1.5 px-2.5 py-1.5 rounded-md border border-zinc-700 bg-zinc-900 text-xs text-zinc-300 cursor-pointer hover:bg-zinc-800 transition-colors"
                >
                  <input
                    type="checkbox"
                    checked={aspectFilterActive}
                    disabled={!hasLeadersAndBase}
                    onChange={(e) => setInAspectOnly(e.target.checked)}
                    className="rounded border-zinc-700 bg-zinc-800 text-sky-500 focus:ring-0"
                  />
                  <span>In-aspect only</span>
                </label>
              )}

              <Button
                variant="outline"
                size="sm"
                onClick={() => setDrawerOpen(true)}
                className="border-zinc-700 bg-zinc-900 hover:bg-zinc-800 hover:text-white gap-1.5"
              >
                <svg className="h-4 w-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                  <polygon
                    points="22 3 2 3 10 12.46 10 19 14 21 14 12.46 22 3"
                    strokeWidth="2"
                    strokeLinecap="round"
                    strokeLinejoin="round"
                  />
                </svg>
                <span>Filters</span>
                {activeFilterCount > 0 && (
                  <Badge
                    variant="secondary"
                    className="bg-sky-600 text-white ml-1 px-1.5 py-0 text-xs"
                  >
                    {activeFilterCount}
                  </Badge>
                )}
              </Button>
            </div>

            {/* Active filter pills */}
            {activeFilterCount > 0 && (
              <div className="flex flex-wrap items-center gap-1.5 text-xs text-zinc-400">
                <span>Active:</span>
                {activeFilters.flatMap((group) =>
                  group.items.map((item) => (
                    <Badge
                      key={`${group.key}-${item.value}`}
                      variant="outline"
                      className="gap-1 border-zinc-700 bg-zinc-900 text-zinc-200"
                    >
                      <span>
                        {group.label}: {item.display}
                      </span>
                      <button
                        type="button"
                        aria-label={`Remove filter ${item.display}`}
                        onClick={() => removeFilterValue(group.key, item.value)}
                        className="hover:text-red-400"
                      >
                        ×
                      </button>
                    </Badge>
                  ))
                )}
                <Button
                  variant="ghost"
                  size="sm"
                  onClick={clearFilters}
                  className="h-6 px-1.5 text-xs text-zinc-400 hover:text-white"
                >
                  Clear all
                </Button>
              </div>
            )}

            {/* Card Results List */}
            <div className="flex-1 min-h-0 overflow-y-auto rounded-lg border border-zinc-800 bg-zinc-900/40">
              {isPrintingsLoading ? (
                <div className="p-8 text-center text-sm text-zinc-400">Loading cards...</div>
              ) : filteredPrintings.length === 0 ? (
                <div className="p-8 text-center text-sm text-zinc-500">
                  {aspectFilterActive
                    ? "No in-aspect cards match your search. Uncheck 'In-aspect only' to browse cards with aspect penalty costs."
                    : "No cards found matching your search."}
                </div>
              ) : (
                <table className="w-full text-left text-sm border-collapse">
                  <thead className="sticky top-0 bg-zinc-900 border-b border-zinc-800 text-xs font-semibold text-zinc-400 z-10">
                    <tr>
                      <th className="py-2 px-3 w-12">Card</th>
                      <th className="py-2 px-3">Name</th>
                      <th className="py-2 px-3 hidden sm:table-cell">Type</th>
                      <th className="py-2 px-3 hidden md:table-cell">Cost</th>
                      <th className="py-2 px-3 text-right">In Deck</th>
                      <th className="py-2 px-3 text-right w-24">Action</th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-zinc-800/60">
                    {filteredPrintings.map((card) => {
                      const printingId =
                        typeof card.printingId === "number"
                          ? card.printingId
                          : Number(card.printingId);
                      const inDeckEntry = deckCardMap.get(printingId);
                      const inDeckQty = inDeckEntry?.quantityInDeck ?? 0;
                      const isHorizontal = isHorizontalPrinting(card);

                      // Calculate aspect penalty for SWU
                      const penalty = isSwu
                        ? calculateAspectPenalty(card.aspects, deckAspects, card.cost ?? 0)
                        : null;

                      // Cap checks
                      const isShield = isSwccg && isDefensiveShield(card);
                      const isShieldCapped = isShield && inDeckQty >= 1;
                      const isSingletonCapped =
                        isSwu && currentFormat === "Twin Suns" && inDeckQty >= 1;
                      const isPremierCapped =
                        isSwu && currentFormat === "Premier" && inDeckQty >= 3;
                      const isAddDisabled = isShieldCapped || isSingletonCapped || isPremierCapped;

                      return (
                        <tr key={printingId} className="hover:bg-zinc-800/40 transition-colors">
                          <td className="py-2 px-3">
                            <div className="flex items-center justify-center w-12 h-11 shrink-0">
                              <CardImageHoverPreview
                                imageUrl={card.imageUrl}
                                cardName={card.cardName}
                                isHorizontal={isHorizontal}
                              />
                            </div>
                          </td>
                          <td className="py-2 px-3">
                            <div className="font-medium text-zinc-200">{card.cardName}</div>
                            {card.subtitle && (
                              <div className="text-xs text-zinc-400">{card.subtitle}</div>
                            )}
                          </td>
                          <td className="py-2 px-3 text-xs text-zinc-400 hidden sm:table-cell">
                            {card.cardTypes?.join(", ") || "—"}
                          </td>
                          <td className="py-2 px-3 text-xs hidden md:table-cell">
                            {isSwu ? (
                              penalty && penalty.penaltyCost > 0 ? (
                                <span
                                  className="text-amber-400 font-semibold"
                                  title={`Base cost ${card.cost ?? 0} + ${penalty.penaltyCost} aspect penalty`}
                                >
                                  {penalty.effectiveCost}{" "}
                                  <span className="text-[10px] text-amber-500/80">
                                    (+{penalty.penaltyCost})
                                  </span>
                                </span>
                              ) : (
                                <span className="text-zinc-300">{card.cost ?? "—"}</span>
                              )
                            ) : (
                              <span className="text-zinc-400">{card.destiny ?? "—"}</span>
                            )}
                          </td>
                          <td className="py-2 px-3 text-right text-xs">
                            {inDeckQty > 0 ? (
                              <span className="font-semibold text-sky-400">×{inDeckQty}</span>
                            ) : (
                              <span className="text-zinc-600">0</span>
                            )}
                          </td>
                          <td className="py-2 px-3 text-right">
                            {inDeckQty > 0 ? (
                              <div className="inline-flex items-center gap-1 bg-zinc-800 rounded px-1 py-0.5 border border-zinc-700">
                                <button
                                  type="button"
                                  onClick={() => handleAdjustQuantity(inDeckEntry!, -1)}
                                  aria-label="Remove 1 copy"
                                  className="w-5 h-5 flex items-center justify-center rounded text-zinc-300 hover:bg-zinc-700 hover:text-white"
                                  title="Remove 1 copy"
                                >
                                  −
                                </button>
                                <span className="w-5 text-center text-xs font-semibold text-sky-300">
                                  {inDeckQty}
                                </span>
                                <button
                                  type="button"
                                  disabled={isAddDisabled}
                                  onClick={() => handleAdjustQuantity(inDeckEntry!, 1)}
                                  aria-label="Add 1 copy"
                                  className="w-5 h-5 flex items-center justify-center rounded text-zinc-300 hover:bg-zinc-700 hover:text-white disabled:opacity-40 disabled:cursor-not-allowed"
                                  title={
                                    isShieldCapped
                                      ? "Max 1 copy per Defensive Shield"
                                      : isSingletonCapped
                                        ? "Singleton limit (max 1 copy in Twin Suns)"
                                        : isPremierCapped
                                          ? "Max 3 copies per card in Premier"
                                          : "Add 1 copy"
                                  }
                                >
                                  +
                                </button>
                              </div>
                            ) : (
                              <Button
                                size="sm"
                                variant="outline"
                                onClick={() => handleAddCard(card)}
                                className="h-7 text-xs border-zinc-700 bg-zinc-800 hover:bg-sky-600 hover:text-white"
                              >
                                + Add
                              </Button>
                            )}
                          </td>
                        </tr>
                      );
                    })}
                  </tbody>
                </table>
              )}
            </div>

            {/* Pagination Controls */}
            <div className="flex justify-between items-center pt-1">
              <span className="text-xs text-zinc-500">
                {totalPrintings > 0
                  ? `Showing ${(filters.page - 1) * filters.pageSize + 1}–${Math.min(
                      filters.page * filters.pageSize,
                      totalPrintings
                    )} of ${totalPrintings} cards`
                  : "0 cards"}
              </span>
              <CardPagination
                page={filters.page}
                totalPages={totalPages}
                pageSize={filters.pageSize}
                onPageChange={(page) => setFilters((prev) => ({ ...prev, page }))}
                onPageSizeChange={(pageSize) =>
                  setFilters((prev) => ({ ...prev, pageSize, page: 1 }))
                }
                className="scale-90 origin-right"
              />
            </div>
          </div>

          {/* Right Column: Deck Cards Sidebar */}
          <div className="lg:col-span-4 flex flex-col min-h-0 bg-zinc-900/30 p-4">
            <div className="flex items-center justify-between pb-3 border-b border-zinc-800">
              <div className="flex items-center gap-2">
                <h3 className="font-semibold text-sm text-zinc-200">Deck Cards</h3>
                <Badge
                  variant="secondary"
                  className="bg-sky-600/30 text-sky-300 border border-sky-500/30 px-2 py-0.5 text-xs"
                >
                  {mainDeckCount} total
                  {isSwccg && defensiveShieldCount > 0 ? ` (+${defensiveShieldCount} shields)` : ""}
                </Badge>
              </div>
              <label className="flex items-center gap-1.5 text-xs text-zinc-400 cursor-pointer">
                <input
                  type="checkbox"
                  checked={includeProxies}
                  onChange={(e) => setIncludeProxies(e.target.checked)}
                  className="rounded border-zinc-700 bg-zinc-800 text-sky-500 focus:ring-0"
                />
                <span>Include proxies</span>
              </label>
            </div>

            {/* Deck Card List */}
            <div className="flex-1 min-h-0 overflow-y-auto divide-y divide-zinc-800/60 pt-2 space-y-4">
              {deckCards.length === 0 ? (
                <div className="py-12 px-4 text-center text-sm text-zinc-500">
                  <p>Your deck is empty.</p>
                  <p className="mt-1 text-xs text-zinc-600">
                    Use the search on the left to add cards to your deck.
                  </p>
                </div>
              ) : (
                Object.entries(groupedDeckCards).map(([groupName, cards]) => {
                  const groupCount = cards.reduce((sum, c) => sum + c.quantityInDeck, 0);
                  return (
                    <div key={groupName} className="space-y-1 pt-2 first:pt-0">
                      <div className="flex items-center justify-between text-xs font-semibold text-zinc-400 px-1 uppercase tracking-wider">
                        <span>{groupName}</span>
                        <span className="text-zinc-500">{groupCount}</span>
                      </div>
                      <div className="space-y-1">
                        {cards.map((card) => {
                          const penalty = isSwu
                            ? calculateAspectPenalty(card.aspects, deckAspects, card.cost)
                            : null;
                          const isShield = isSwccg && isDefensiveShield(card);

                          return (
                            <div
                              key={card.printingId}
                              className="flex items-center justify-between gap-2 p-1.5 rounded-lg bg-zinc-900/70 hover:bg-zinc-800/80 border border-zinc-800/80 transition-colors"
                            >
                              <div className="flex items-center gap-2 min-w-0">
                                <div className="flex items-center justify-center w-12 h-11 shrink-0">
                                  <CardImageHoverPreview
                                    imageUrl={card.imageUrl}
                                    cardName={card.cardName}
                                    isHorizontal={card.isHorizontal}
                                  />
                                </div>
                                <div className="min-w-0">
                                  <div
                                    className="text-xs font-medium text-zinc-200 truncate"
                                    title={card.cardName}
                                  >
                                    {card.cardName}
                                  </div>
                                  <div className="flex items-center gap-2 text-[10px] text-zinc-500">
                                    {isSwu && penalty && penalty.penaltyCost > 0 && (
                                      <span className="text-amber-400">
                                        Cost: {penalty.effectiveCost} (+{penalty.penaltyCost})
                                      </span>
                                    )}
                                    <span>
                                      Avail:{" "}
                                      {includeProxies
                                        ? card.availabilityWithProxies
                                        : card.availability}
                                    </span>
                                  </div>
                                </div>
                              </div>

                              <div className="flex items-center gap-1.5 shrink-0">
                                {/* SWCCG Starting Card Toggle Button */}
                                {isSwccg && !isShield && (
                                  <button
                                    type="button"
                                    onClick={() => handleToggleStartingCard(card)}
                                    className={`px-1.5 py-0.5 rounded text-[10px] font-semibold border transition-all ${
                                      card.isStarting
                                        ? "border-amber-400/60 bg-amber-500/20 text-amber-300"
                                        : "border-zinc-800 bg-zinc-900 text-zinc-500 hover:text-zinc-300 hover:border-zinc-700"
                                    }`}
                                    title={
                                      card.isStarting
                                        ? "Marked as Starting Card"
                                        : "Click to mark as Starting Card"
                                    }
                                  >
                                    {card.isStarting ? "★ Start" : "☆ Start"}
                                  </button>
                                )}

                                <div className="flex items-center bg-zinc-800 rounded border border-zinc-700">
                                  <button
                                    type="button"
                                    onClick={() => handleAdjustQuantity(card, -1)}
                                    aria-label={`Decrease ${card.cardName} quantity`}
                                    className="w-5 h-5 flex items-center justify-center text-xs text-zinc-300 hover:bg-zinc-700 hover:text-white"
                                    title="Decrement"
                                  >
                                    −
                                  </button>
                                  <span className="w-5 text-center text-xs font-bold text-sky-400">
                                    {card.quantityInDeck}
                                  </span>
                                  <button
                                    type="button"
                                    disabled={isShield && card.quantityInDeck >= 1}
                                    onClick={() => handleAdjustQuantity(card, 1)}
                                    aria-label={`Increase ${card.cardName} quantity`}
                                    className="w-5 h-5 flex items-center justify-center text-xs text-zinc-300 hover:bg-zinc-700 hover:text-white disabled:opacity-40 disabled:cursor-not-allowed"
                                    title={
                                      isShield && card.quantityInDeck >= 1
                                        ? "Max 1 per Defensive Shield"
                                        : "Increment"
                                    }
                                  >
                                    +
                                  </button>
                                </div>

                                <button
                                  type="button"
                                  onClick={() => handleRemoveCard(card)}
                                  aria-label={`Remove ${card.cardName} from deck`}
                                  className="w-5 h-5 flex items-center justify-center rounded text-zinc-500 hover:text-red-400 hover:bg-red-950/30 transition-colors"
                                  title="Remove from deck"
                                >
                                  ×
                                </button>
                              </div>
                            </div>
                          );
                        })}
                      </div>
                    </div>
                  );
                })
              )}
            </div>
          </div>
        </div>
      </div>

      {/* Format Switch Guidance Dialog (e.g. Twin Suns -> Premier with 2 leaders) */}
      {switchingFormat === "Premier" && leadersInDeck.length > 1 && (
        <div
          role="dialog"
          aria-modal="true"
          className="fixed inset-0 z-[60] flex items-center justify-center p-4 bg-black/85 backdrop-blur-sm"
        >
          <div className="w-full max-w-md rounded-xl border border-zinc-800 bg-zinc-900 p-6 text-zinc-100 shadow-2xl space-y-4">
            <h3 className="text-lg font-bold text-white">Switch to Premier Format</h3>
            <p className="text-sm text-zinc-300">
              Premier format requires <strong>exactly 1 Leader</strong>. Your deck currently has 2
              leaders. Please choose which leader to keep:
            </p>

            <div className="space-y-2">
              {leadersInDeck.map((l) => (
                <label
                  key={l.printingId}
                  className={`flex items-center gap-3 p-3 rounded-lg border cursor-pointer transition-all ${
                    leaderToKeep === l.printingId
                      ? "border-sky-500 bg-sky-950/40 text-white ring-1 ring-sky-500"
                      : "border-zinc-800 bg-zinc-950 text-zinc-300 hover:border-zinc-700"
                  }`}
                >
                  <input
                    type="radio"
                    name="keep-leader"
                    checked={leaderToKeep === l.printingId}
                    onChange={() => setLeaderToKeep(l.printingId)}
                    className="text-sky-500 focus:ring-0"
                  />
                  <CardImageHoverPreview
                    imageUrl={l.imageUrl}
                    cardName={l.cardName}
                    isHorizontal={l.isHorizontal}
                    thumbnailWidth={30}
                    thumbnailHeight={40}
                  />
                  <span className="font-semibold text-sm">{l.cardName}</span>
                </label>
              ))}
            </div>

            <div className="flex justify-end gap-3 pt-2">
              <Button
                variant="outline"
                size="sm"
                onClick={() => setSwitchingFormat(null)}
                className="border-zinc-700 bg-zinc-800 text-zinc-300"
              >
                Cancel
              </Button>
              <Button
                size="sm"
                onClick={handleConfirmLeaderRemoval}
                className="bg-sky-600 hover:bg-sky-500 text-white"
              >
                Keep Selected &amp; Switch
              </Button>
            </div>
          </div>
        </div>
      )}

      {/* Format Switch Guidance Dialog (Decipher Only with Virtual cards) */}
      {switchingFormat === "Decipher Only" && virtualCardsToRemove.length > 0 && (
        <div
          role="dialog"
          aria-modal="true"
          className="fixed inset-0 z-[60] flex items-center justify-center p-4 bg-black/85 backdrop-blur-sm"
        >
          <div className="w-full max-w-md rounded-xl border border-zinc-800 bg-zinc-900 p-6 text-zinc-100 shadow-2xl space-y-4">
            <h3 className="text-lg font-bold text-white">Switch to Decipher Only</h3>
            <p className="text-sm text-zinc-300">
              Decipher Only format does not allow Virtual set cards. The following{" "}
              {virtualCardsToRemove.length} card(s) will be removed:
            </p>
            <ul className="max-h-36 overflow-y-auto space-y-1 text-xs text-zinc-400 bg-zinc-950 p-2.5 rounded border border-zinc-800">
              {virtualCardsToRemove.map((c) => (
                <li key={c.printingId} className="flex justify-between">
                  <span>{c.cardName}</span>
                  <span className="text-zinc-500">×{c.quantityInDeck}</span>
                </li>
              ))}
            </ul>

            <div className="flex justify-end gap-3 pt-2">
              <Button
                variant="outline"
                size="sm"
                onClick={() => setSwitchingFormat(null)}
                className="border-zinc-700 bg-zinc-800 text-zinc-300"
              >
                Cancel
              </Button>
              <Button
                size="sm"
                onClick={handleConfirmVirtualRemoval}
                className="bg-red-600 hover:bg-red-500 text-white"
              >
                Remove Cards &amp; Switch
              </Button>
            </div>
          </div>
        </div>
      )}

      {/* Filter Drawer */}
      <CardFiltersDrawer
        open={drawerOpen}
        onOpenChange={setDrawerOpen}
        filters={filters}
        setFilters={setFilters}
        availableFacets={
          facets ?? {
            games: [currentGame],
            sets: [],
            rarities: [],
            types: [],
            arenas: [],
            aspects: [],
            traits: [],
            keywords: [],
          }
        }
        isFacetsLoading={isFacetsLoading}
        isFacetsError={isFacetsError}
        onRetryFacets={refetchFacets}
        onClearFilters={clearFilters}
        hasActiveFilters={activeFilterCount > 0}
        game={currentGame}
      />
    </div>,
    document.body
  );
}
