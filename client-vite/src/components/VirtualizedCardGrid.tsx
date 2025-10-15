import { useEffect, useMemo, useRef, useState, type ReactNode } from "react";
import { useVirtualizer } from "@tanstack/react-virtual";
import CardTile, { CardSummary } from "./CardTile";

// props
type Props = {
  items: CardSummary[];
  isFetchingNextPage: boolean;
  hasNextPage?: boolean;
  fetchNextPage?: () => void;
  onCardClick?: (c: CardSummary) => void;
  renderItem?: (card: CardSummary) => ReactNode;
  minTileWidth?: number; // px; default 220
  rowGap?: number; // px; default 12
  colGap?: number; // px; default 12
  overscan?: number; // rows; default 6
  footerHeight?: number; // px; default 0 (changed from 88 after removing text from card tiles)
};

export default function VirtualizedCardGrid({
  items,
  isFetchingNextPage,
  hasNextPage,
  fetchNextPage,
  onCardClick,
  renderItem,
  minTileWidth = 220,
  rowGap = 12,
  colGap = 12,
  overscan = 6,
  footerHeight = 0,
}: Props) {
  const scrollRef = useRef<HTMLDivElement | null>(null);
  const [containerWidth, setContainerWidth] = useState(0);

  useEffect(() => {
    const el = scrollRef.current;
    if (!el) return;
    const ro = new ResizeObserver((entries) => {
      setContainerWidth(entries[0].contentRect.width);
    });
    ro.observe(el);
    return () => ro.disconnect();
  }, []);

  const columns = useMemo(() => {
    if (containerWidth <= 0) return 1;
    const cols = Math.floor((containerWidth + colGap) / (minTileWidth + colGap));
    return Number.isFinite(cols) && cols > 0 ? cols : 1;
  }, [containerWidth, minTileWidth, colGap]);

  const tileWidth = useMemo(() => {
    // In single-column mode, we allow the card to expand and fill the entire container width
    // for better aesthetics and use of space, rather than clamping it to minTileWidth.
    // If this is not desired, replace 'containerWidth' with 'minTileWidth' below.
    if (columns <= 1) return Math.max(1, Math.floor(containerWidth));
    const totalGap = colGap * (columns - 1);
    const w = Math.floor((containerWidth - totalGap) / columns);
    return w > 0 ? w : minTileWidth;
  }, [columns, containerWidth, colGap, minTileWidth]);

  // 3:4 image + footer
  const tileHeight = Math.floor((tileWidth * 4) / 3) + footerHeight;

  const rowCount = Math.ceil(items.length / columns);

  const rowVirtualizer = useVirtualizer({
    count: rowCount,
    getScrollElement: () => scrollRef.current,
    estimateSize: () => tileHeight + rowGap,
    overscan,
  });

  useEffect(() => {
    if (!hasNextPage || !fetchNextPage) return;
    const vItems = rowVirtualizer.getVirtualItems();
    if (vItems.length === 0) return;
    const last = vItems[vItems.length - 1];
    if (last.index >= rowCount - 3 && !isFetchingNextPage) {
      fetchNextPage();
    }
  }, [rowVirtualizer.getVirtualItems(), rowCount, hasNextPage, fetchNextPage, isFetchingNextPage]); // eslint-disable-line react-hooks/exhaustive-deps

  return (
    <div ref={scrollRef} className="h-full w-full overflow-auto">
      <div className="relative" style={{ height: rowVirtualizer.getTotalSize() }}>
        {rowVirtualizer.getVirtualItems().map((vRow) => {
          const rowIndex = vRow.index;
          const startY = vRow.start;
          const start = rowIndex * columns;
          const end = Math.min(start + columns, items.length);
          const rowItems = items.slice(start, end);

          return (
            <div
              key={rowIndex}
              className="absolute left-0 w-full"
              style={{ transform: `translateY(${startY}px)` }}
            >
              <div
                className="grid"
                style={{
                  gridTemplateColumns: `repeat(${columns}, minmax(0, 1fr))`,
                  gap: `${rowGap}px ${colGap}px`,
                }}
              >
                {rowItems.map((card) => (
                  <div key={card.id} style={{ height: tileHeight }}>
                    {renderItem ? renderItem(card) : <CardTile card={card} onClick={onCardClick} />}
                  </div>
                ))}
              </div>
            </div>
          );
        })}
      </div>

      {isFetchingNextPage && (
        <div className="flex items-center justify-center py-4 text-sm text-muted-foreground">
          Loading more…
        </div>
      )}
    </div>
  );
}
