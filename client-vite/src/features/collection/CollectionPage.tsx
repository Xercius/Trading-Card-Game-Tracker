import { useEffect, useMemo, useRef } from "react";
import { useVirtualizer } from "@tanstack/react-virtual";
import { useUser } from "@/state/useUser";
import { useQueryState } from "@/hooks/useQueryState";
import { DEFAULT_PAGE_SIZE } from "@/constants";
import { getAvailabilityDisplay, formatAvailability } from "@/lib/availability";
import { useIncludeProxies } from "@/hooks/useIncludeProxies";
import { collectionKeys, useCollectionQuery } from "./api";
import QtyField from "./QtyField";
import BulkAddDialog from "./BulkAddDialog";
import LineSparkline from "@/components/charts/LineSparkline";
import { useCollectionValueHistory } from "./useCollectionValueHistory";
import { latestValue } from "@/lib/valueHistory";

function formatCurrency(value: number | null): string {
  if (value == null || Number.isNaN(value)) return "—";
  return `$${value.toFixed(2)}`;
}

const ESTIMATED_ROW_HEIGHT = 72;

export default function CollectionPage() {
  const { userId } = useUser();
  const [q, setQ] = useQueryState("q", "");
  const [game, setGame] = useQueryState("game", "");
  const [setName, setSetName] = useQueryState("set", "");
  const [rarity, setRarity] = useQueryState("rarity", "");
  const [pageParam, setPageParam] = useQueryState("page", "1");
  const [pageSizeParam, setPageSizeParam] = useQueryState("pageSize", String(DEFAULT_PAGE_SIZE));
  const [includeProxies, setIncludeProxies] = useIncludeProxies();

  const page = useMemo(() => {
    const parsed = Number(pageParam);
    return Number.isFinite(parsed) && parsed > 0 ? Math.floor(parsed) : 1;
  }, [pageParam]);

  const pageSize = useMemo(() => {
    const parsed = Number(pageSizeParam);
    return Number.isFinite(parsed) && parsed > 0 ? Math.floor(parsed) : DEFAULT_PAGE_SIZE;
  }, [pageSizeParam]);

  useEffect(() => {
    setPageParam("1");
  }, [q, game, setName, rarity]);

  const queryKey = collectionKeys.list({
    userId,
    page,
    pageSize,
    filters: { q, game, set: setName, rarity },
    includeProxies,
  });

  const { data, isPending, isError, refetch, isFetching } = useCollectionQuery({
    userId,
    page,
    pageSize,
    filters: { q, game, set: setName, rarity },
    includeProxies,
  });

  const items = data?.items ?? [];
  const total = data?.total ?? 0;

  const valueHistoryQuery = useCollectionValueHistory(undefined, !!userId);
  const valueHistoryPoints = valueHistoryQuery.data ?? [];
  const collectionLatestValue = useMemo(() => latestValue(valueHistoryPoints), [valueHistoryPoints]);

  const tableRef = useRef<HTMLDivElement | null>(null);
  const rowVirtualizer = useVirtualizer({
    count: items.length,
    getScrollElement: () => tableRef.current,
    estimateSize: () => ESTIMATED_ROW_HEIGHT,
    overscan: 8,
  });

  const handlePrev = () => {
    setPageParam(String(Math.max(1, page - 1)));
  };

  const handleNext = () => {
    setPageParam(String(page + 1));
  };

  if (!userId) {
    return <div className="p-6 text-sm text-muted-foreground">Select a user to view their collection.</div>;
  }

  if (isError) {
    return (
      <div className="flex flex-col gap-4 p-6">
        <p className="text-sm text-destructive">Failed to load collection.</p>
        <button type="button" className="w-fit rounded border border-input bg-background px-3 py-2 text-sm" onClick={() => refetch()}>
          Retry
        </button>
      </div>
    );
  }

  return (
    <div className="flex h-full flex-col gap-4 p-6">
      <div className="flex flex-wrap items-end gap-4">
        <div className="flex flex-col gap-2">
          <label className="text-xs font-medium uppercase text-muted-foreground" htmlFor="collection-q">
            Search
          </label>
          <input
            id="collection-q"
            value={q}
            onChange={(event) => setQ(event.target.value)}
            className="w-60 rounded border border-input bg-background px-3 py-2 text-sm"
            placeholder="Name contains…"
          />
        </div>
        <div className="flex flex-col gap-2">
          <label className="text-xs font-medium uppercase text-muted-foreground" htmlFor="collection-game">
            Game
          </label>
          <input
            id="collection-game"
            value={game}
            onChange={(event) => setGame(event.target.value)}
            className="w-40 rounded border border-input bg-background px-3 py-2 text-sm"
            placeholder="Magic"
          />
        </div>
        <div className="flex flex-col gap-2">
          <label className="text-xs font-medium uppercase text-muted-foreground" htmlFor="collection-set">
            Set
          </label>
          <input
            id="collection-set"
            value={setName}
            onChange={(event) => setSetName(event.target.value)}
            className="w-40 rounded border border-input bg-background px-3 py-2 text-sm"
            placeholder="Alpha"
          />
        </div>
        <div className="flex flex-col gap-2">
          <label className="text-xs font-medium uppercase text-muted-foreground" htmlFor="collection-rarity">
            Rarity
          </label>
          <input
            id="collection-rarity"
            value={rarity}
            onChange={(event) => setRarity(event.target.value)}
            className="w-32 rounded border border-input bg-background px-3 py-2 text-sm"
            placeholder="Rare"
          />
        </div>
        <div className="flex items-center gap-2 text-sm">
          <label htmlFor="include-proxies" className="flex items-center gap-2">
            <input
              id="include-proxies"
              type="checkbox"
              checked={includeProxies}
              onChange={(event) => setIncludeProxies(event.target.checked)}
            />
            Include proxies in availability
          </label>
        </div>
        <BulkAddDialog queryKey={queryKey} />
      </div>

      <section className="rounded border border-border bg-card p-4">
        <div className="flex items-center justify-between gap-4">
          <div>
            <h2 className="text-sm font-semibold">Collection value (last 90 days)</h2>
            {collectionLatestValue != null && (
              <p className="text-xs text-muted-foreground">Latest: {formatCurrency(collectionLatestValue)}</p>
            )}
          </div>
          <span className="text-xs text-muted-foreground">Proxies excluded.</span>
        </div>
        <div className="mt-3">
          {valueHistoryQuery.isLoading && (
            <p className="text-xs text-muted-foreground">Loading value history…</p>
          )}
          {valueHistoryQuery.isError && (
            <p className="text-xs text-destructive">Failed to load value history.</p>
          )}
          {!valueHistoryQuery.isLoading && !valueHistoryQuery.isError && valueHistoryPoints.length === 0 && (
            <p className="text-xs text-muted-foreground">No value data.</p>
          )}
          {valueHistoryPoints.length > 0 && (
            <LineSparkline
              points={valueHistoryPoints}
              ariaLabel="Collection value over time"
              height={80}
              className="h-20"
              stroke="hsl(var(--primary))"
            />
          )}
        </div>
      </section>

      <div className="flex items-center justify-between text-xs text-muted-foreground">
        <div>
          Page {page} · Showing {items.length} of {total} cards {isFetching && <span className="ml-2">Refreshing…</span>}
        </div>
        <div className="flex items-center gap-2">
          <button
            type="button"
            onClick={handlePrev}
            disabled={page <= 1 || isPending}
            className="rounded border border-input bg-background px-2 py-1 text-sm disabled:opacity-50"
          >
            Prev
          </button>
          <button
            type="button"
            onClick={handleNext}
            disabled={items.length < pageSize || isPending}
            className="rounded border border-input bg-background px-2 py-1 text-sm disabled:opacity-50"
          >
            Next
          </button>
        </div>
      </div>

      <div className="flex-1 overflow-hidden rounded border border-border">
        {isPending ? (
          <div className="flex h-full items-center justify-center text-sm text-muted-foreground">Loading collection…</div>
        ) : items.length === 0 ? (
          <div className="flex h-full items-center justify-center text-sm text-muted-foreground">No cards matched your filters.</div>
        ) : (
          <div className="flex h-full flex-col">
            <div className="grid grid-cols-[2fr,1.2fr,0.8fr,0.8fr,0.8fr,0.8fr,1fr] border-b bg-muted/50 px-4 py-3 text-xs font-semibold uppercase tracking-wide text-muted-foreground">
              <span>Card</span>
              <span>Set</span>
              <span>Rarity</span>
              <span>Owned</span>
              <span>Proxy</span>
              <span>Wishlist</span>
              <span>Availability</span>
            </div>
            <div ref={tableRef} className="relative flex-1 overflow-auto">
              <div style={{ height: rowVirtualizer.getTotalSize() }}>
                {rowVirtualizer.getVirtualItems().map((virtualRow) => {
                  const item = items[virtualRow.index];
                  const availability = getAvailabilityDisplay(item.availability, item.availabilityWithProxies, includeProxies);
                  return (
                    <div
                      key={item.cardPrintingId}
                      className="absolute left-0 right-0 border-b border-border px-4 py-3 text-sm"
                      style={{ transform: `translateY(${virtualRow.start}px)` }}
                    >
                      <div className="grid grid-cols-[2fr,1.2fr,0.8fr,0.8fr,0.8fr,0.8fr,1fr] items-center gap-2">
                        <div>
                          <div className="font-medium">{item.cardName}</div>
                          <div className="text-xs text-muted-foreground">#{item.number} · {item.game}</div>
                        </div>
                        <div className="text-sm text-muted-foreground">{item.set}</div>
                        <div className="text-sm text-muted-foreground">{item.rarity}</div>
                        <QtyField
                          field="owned"
                          printingId={item.cardPrintingId}
                          value={item.quantityOwned}
                          otherValue={item.quantityProxyOwned}
                          queryKey={queryKey}
                        />
                        <QtyField
                          field="proxy"
                          printingId={item.cardPrintingId}
                          value={item.quantityProxyOwned}
                          otherValue={item.quantityOwned}
                          queryKey={queryKey}
                        />
                        <div className="text-center text-sm">{item.quantityWanted}</div>
                        <div className="flex items-center justify-start">
                          <span className="inline-flex items-center gap-2 rounded-full bg-muted px-3 py-1 text-xs">
                            <span className="font-semibold">{availability.label}</span>
                            <span>{formatAvailability(availability.value)}</span>
                          </span>
                        </div>
                      </div>
                    </div>
                  );
                })}
              </div>
            </div>
          </div>
        )}
      </div>
    </div>
  );
}
