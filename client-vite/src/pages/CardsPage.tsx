import { useCallback, useEffect, useMemo, useState } from "react";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import {
  usePrintings,
  type PrintingDto,
  type PrintingsQuery,
} from "@/features/cards/api/list";
import FiltersRail from "@/features/cards/filters/FiltersRail";
import { useCardFilters } from "@/features/cards/filters/useCardFilters";
import CardModal from "@/features/cards/components/CardModal";
import { ActiveFilters } from "@/features/cards/components/ActiveFilters";
import { useUser } from "@/state/useUser";

export default function CardsPage() {
  const { userId } = useUser();
  const { filters, setFilters, clearAll } = useCardFilters();
  const [isMobileFiltersOpen, setMobileFiltersOpen] = useState(false);
  const [selectedCardId, setSelectedCardId] = useState<number | null>(null);
  const [selectedPrintingId, setSelectedPrintingId] = useState<number | null>(null);

  const [game, setGame] = useState<string | undefined>();
  const [setName, setSetName] = useState<string | undefined>();
  const [rarity, setRarity] = useState<string | undefined>();
  const [style, setStyle] = useState<string | undefined>();
  const [qtext, setQtext] = useState<string | undefined>();

  useEffect(() => {
    const csv = filters.games.join(",");
    setGame(csv.length > 0 ? csv : undefined);
  }, [filters.games]);

  useEffect(() => {
    const csv = filters.sets.join(",");
    setSetName(csv.length > 0 ? csv : undefined);
  }, [filters.sets]);

  useEffect(() => {
    const csv = filters.rarities.join(",");
    setRarity(csv.length > 0 ? csv : undefined);
  }, [filters.rarities]);

  useEffect(() => {
    const trimmed = filters.q.trim();
    setQtext(trimmed.length > 0 ? trimmed : undefined);
  }, [filters.q]);

  const displayGame = useMemo(() => filters.games.join(", "), [filters.games]);
  const displaySet = useMemo(() => filters.sets.join(", "), [filters.sets]);
  const displayRarity = useMemo(
    () => filters.rarities.join(", "),
    [filters.rarities]
  );

  const query = useMemo<PrintingsQuery>(() => {
    const trimmedStyle = style?.trim();
    const trimmedQ = qtext?.trim();
    return {
      game,
      set: setName,
      rarity,
      style: trimmedStyle && trimmedStyle.length > 0 ? trimmedStyle : undefined,
      q: trimmedQ && trimmedQ.length > 0 ? trimmedQ : undefined,
      page: 1,
      pageSize: 120,
    };
  }, [game, setName, rarity, style, qtext]);

  const {
    data: printings = [],
    isLoading,
    isError,
    isFetching,
  } = usePrintings(query);

  const handleModalOpenChange = useCallback((open: boolean) => {
    if (!open) {
      setSelectedCardId(null);
      setSelectedPrintingId(null);
    }
  }, []);

  const handlePrintingClick = useCallback((printing: PrintingDto) => {
    if (!userId) return;
    setSelectedCardId(printing.cardId);
    setSelectedPrintingId(printing.printingId);
  }, [userId]);

  useEffect(() => {
    if (selectedCardId == null) return;
    const stillExists = printings.some((printing) => printing.cardId === selectedCardId);
    if (!stillExists) {
      setSelectedCardId(null);
      setSelectedPrintingId(null);
    }
  }, [printings, selectedCardId]);

  const hasNoResults = !isLoading && !isFetching && printings.length === 0;

  const handleClearFilter = useCallback(
    (key?: string) => {
      if (!key) {
        setGame(undefined);
        setSetName(undefined);
        setRarity(undefined);
        setStyle(undefined);
        setQtext(undefined);
        clearAll();
        return;
      }

      const filterHandlers: Record<string, () => void> = {
        game: () => {
          setGame(undefined);
          setFilters((prev) => ({ ...prev, games: [] }));
        },
        set: () => {
          setSetName(undefined);
          setFilters((prev) => ({ ...prev, sets: [] }));
        },
        rarity: () => {
          setRarity(undefined);
          setFilters((prev) => ({ ...prev, rarities: [] }));
        },
        style: () => {
          setStyle(undefined);
        },
        q: () => {
          setQtext(undefined);
          setFilters((prev) => ({ ...prev, q: "" }));
        },
      };

      const handler = filterHandlers[key];
      if (handler) {
        handler();
      }
    },
    [clearAll, setFilters]
  );

  const styleDisplay = style?.trim();

  return (
    <div className="flex h-[calc(100vh-64px)] bg-background">
      <aside className="hidden w-72 shrink-0 border-r bg-background lg:block">
        <FiltersRail onClearAll={() => setStyle(undefined)} />
      </aside>
      <div className="flex flex-1 flex-col overflow-hidden">
        <div className="flex items-center justify-between gap-2 border-b px-3 py-2 lg:hidden">
          <Button
            variant="outline"
            size="sm"
            onClick={() => setMobileFiltersOpen(true)}
            aria-label="Open filters"
          >
            Filters
          </Button>
        </div>
        <div className="flex-1 overflow-hidden px-3 pb-3 pt-2 lg:px-4 lg:pb-4 lg:pt-4">
          <div className="flex h-full flex-col">
            <div className="mb-3 flex flex-wrap items-end gap-4">
              <div className="flex flex-col gap-1">
                <label
                  htmlFor="printing-style-filter"
                  className="text-xs font-semibold uppercase tracking-wide text-muted-foreground"
                >
                  Style
                </label>
                <Input
                  id="printing-style-filter"
                  value={style ?? ""}
                  onChange={(event) => {
                    const value = event.target.value;
                    setStyle(value.length > 0 ? value : undefined);
                  }}
                  placeholder="Any style"
                  className="w-48"
                  aria-label="Filter by style"
                />
              </div>
            </div>
            <ActiveFilters
              filters={{
                game: displayGame.length > 0 ? displayGame : undefined,
                set: displaySet.length > 0 ? displaySet : undefined,
                rarity: displayRarity.length > 0 ? displayRarity : undefined,
                style: styleDisplay && styleDisplay.length > 0 ? styleDisplay : undefined,
                q: qtext,
              }}
              onClear={handleClearFilter}
            />
            {isError && (
              <div className="mb-3 rounded-md border border-destructive/40 bg-destructive/10 p-3 text-sm text-destructive-foreground">
                Error loading printings
              </div>
            )}
            {hasNoResults && (
              <div className="mb-3 rounded-md border p-4 text-sm">No printings found</div>
            )}
            <div className="mt-3 flex-1 overflow-y-auto rounded-lg border bg-card p-3">
              {isLoading ? (
                <div className="p-4 text-sm">Loading…</div>
              ) : (
                <ul className="grid grid-cols-2 gap-4 sm:grid-cols-3 md:grid-cols-4 lg:grid-cols-6">
                  {printings.map((printing) => (
                    <li
                      key={printing.printingId}
                      className="overflow-hidden rounded border bg-white/5 shadow-sm transition hover:shadow-md"
                    >
                      <button
                        type="button"
                        onClick={() => handlePrintingClick(printing)}
                        className="w-full text-left"
                        disabled={!userId}
                      >
                        <img
                          src={printing.imageUrl}
                          alt={`${printing.cardName} (${printing.setName} #${printing.number})`}
                          className="aspect-[3/4] w-full object-cover"
                          loading="lazy"
                        />
                        <div className="space-y-1 p-2 text-sm">
                          <div className="font-medium">{printing.cardName}</div>
                          <div className="text-xs text-muted-foreground">
                            {printing.game} • {printing.setName} • #{printing.number} • {printing.rarity}
                          </div>
                        </div>
                      </button>
                    </li>
                  ))}
                </ul>
              )}
            </div>
          </div>
        </div>
      </div>
      {isMobileFiltersOpen && (
        <div className="fixed inset-0 z-50 flex lg:hidden" role="dialog" aria-modal="true">
          <button
            type="button"
            className="flex-1 bg-black/40"
            aria-label="Close filters overlay"
            onClick={() => setMobileFiltersOpen(false)}
          />
          <div className="h-full w-80 max-w-full bg-background shadow-xl">
            <FiltersRail
              onClose={() => setMobileFiltersOpen(false)}
              onClearAll={() => setStyle(undefined)}
            />
          </div>
        </div>
      )}
      {selectedCardId != null && userId && (
        <CardModal
          open={true}
          cardId={selectedCardId}
          initialPrintingId={selectedPrintingId ?? undefined}
          onOpenChange={handleModalOpenChange}
        />
      )}
    </div>
  );
}
