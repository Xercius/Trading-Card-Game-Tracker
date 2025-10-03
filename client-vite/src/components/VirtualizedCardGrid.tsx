import { useEffect, useMemo, useRef, useState } from "react";
import { useVirtualizer } from "@tanstack/react-virtual";
import CardTile, { CardSummary } from "./CardTile";

type Props = {
  items: CardSummary[];
  isFetchingNextPage: boolean;
  hasNextPage?: boolean;
  fetchNextPage?: () => void;
  onCardClick?: (c: CardSummary) => void;
  minTileWidth?: number; // px; default 220
  rowGap?: number; // px; default 12
  colGap?: number; // px; default 12
};

export default function VirtualizedCardGrid({
  items,
  isFetchingNextPage,
  hasNextPage,
  fetchNextPage,
  onCardClick,
  minTileWidth = 220,
  rowGap = 12,
  colGap = 12,
}: Props) {
  const scrollRef = useRef<HTMLDivElement | null>(null);
  const [containerWidth, setContainerWidth] = useState(0);

  // Track container width to compute columns responsively
  useEffect(() => {
    const el = scrollRef.current;
    if (!el) return;
    const ro = new ResizeObserver((entries) => {
      const w = entries[0].contentRect.width;
      setContainerWidth(w);
    });
    ro.observe(el);
    return () => ro.disconnect();
  }, []);

  const columns = useMemo(() => {
    if (containerWidth <= 0) return 1;
    const cols = Math.max(1, Math.floor((containerWidth + colGap) / (minTileWidth + colGap)));
    return cols;
  }, [containerWidth, minTileWidth, colGap]);

  const tileWidth = useMemo(() => {
    if (columns === 0) return minTileWidth;
    const totalGap = colGap * (columns - 1);
    const w = Math.floor((containerWidth - totalGap) / columns);
    return w;
  }, [columns, containerWidth, colGap, minTileWidth]);

  // 3:4 aspect + header area ~88px
  const tileHeight = Math.floor((tileWidth * 4) / 3) + 88;

  const rowCount = Math.ceil(items.length / columns);

  const rowVirtualizer = useVirtualizer({
    count: rowCount,
    getScrollElement: () => scrollRef.current,
    estimateSize: () => tileHeight + rowGap,
    overscan: 10,
  });

  // Auto-load next page when last row comes into view
  useEffect(() => {
    if (!hasNextPage || !fetchNextPage) return;
    const vItems = rowVirtualizer.getVirtualItems();
    if (vItems.length === 0) return;
    const last = vItems[vItems.length - 1];
    if (last.index >= rowCount - 3 && !isFetchingNextPage) {
      fetchNextPage();
    }
  }, [rowVirtualizer, rowCount, hasNextPage, fetchNextPage, isFetchingNextPage]);

  return (
    <div ref={scrollRef} className="h-full w-full overflow-auto">
      <div
        className="relative"
        style={{ height: rowVirtualizer.getTotalSize() }}
      >
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
                    <CardTile card={card} onClick={onCardClick} />
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
