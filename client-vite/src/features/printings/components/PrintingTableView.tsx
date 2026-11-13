import { useRef } from "react";
import { useVirtualizer } from "@tanstack/react-virtual";
import type { PrintingListItem } from "@/features/printings/api/printings";

type Props = {
  printings: PrintingListItem[];
  onRowClick?: (p: PrintingListItem) => void;
};

const VIRTUALIZATION_THRESHOLD = 50;
const ESTIMATED_ROW_HEIGHT = 41;

function PrintingTableRow({
  printing,
  onRowClick,
}: {
  printing: PrintingListItem;
  onRowClick?: (p: PrintingListItem) => void;
}) {
  return (
    <tr
      className={
        onRowClick
          ? "border-t cursor-pointer hover:bg-muted/50 transition-colors focus:outline-none focus:bg-muted/70"
          : "border-t"
      }
      onClick={() => onRowClick?.(printing)}
      tabIndex={onRowClick ? 0 : undefined}
      role={onRowClick ? "button" : undefined}
      onKeyDown={
        onRowClick
          ? (e) => {
              if (e.key === "Enter" || e.key === " ") {
                e.preventDefault();
                onRowClick(printing);
              }
            }
          : undefined
      }
    >
      <td className="px-3 py-2">{printing.cardName}</td>
      <td className="px-3 py-2">{printing.game}</td>
      <td className="px-3 py-2">{printing.setName}</td>
      <td className="px-3 py-2">{printing.setCode ?? "—"}</td>
      <td className="px-3 py-2">{printing.number ?? "—"}</td>
      <td className="px-3 py-2">{printing.rarity ?? "—"}</td>
      <td className="px-3 py-2">{printing.cardTypes?.join(", ") || "—"}</td>
      <td className="px-3 py-2">
        {printing.aspects
          ?.map(({ name, count }) => `${name}${count > 1 ? ` ×${count}` : ""}`)
          .join(", ") || "—"}
      </td>
      <td className="px-3 py-2">{printing.releaseDate ?? "—"}</td>
    </tr>
  );
}

/**
 * Displays card printings in a table, virtualizing rows automatically when more than 50 rows are rendered.
 */
export function PrintingTableView({ printings, onRowClick }: Props) {
  const scrollRef = useRef<HTMLDivElement | null>(null);
  const shouldVirtualize = printings.length > VIRTUALIZATION_THRESHOLD;

  const virtualizer = useVirtualizer({
    count: shouldVirtualize ? printings.length : 0,
    getScrollElement: () => scrollRef.current,
    estimateSize: () => ESTIMATED_ROW_HEIGHT,
    overscan: 8,
  });

  const virtualItems = shouldVirtualize ? virtualizer.getVirtualItems() : [];
  const paddingTop = virtualItems.length > 0 ? virtualItems[0].start : 0;
  const paddingBottom =
    virtualItems.length > 0
      ? virtualizer.getTotalSize() - virtualItems[virtualItems.length - 1].end
      : 0;

  return (
    <div
      ref={scrollRef}
      className={
        shouldVirtualize
          ? "max-h-[600px] overflow-auto rounded-md border"
          : "overflow-x-auto rounded-md border"
      }
    >
      <table className="w-full text-sm">
        <thead className="sticky top-0 z-10 bg-muted text-muted-foreground">
          <tr>
            <th className="px-3 py-2 text-left font-medium">Name</th>
            <th className="px-3 py-2 text-left font-medium">Game</th>
            <th className="px-3 py-2 text-left font-medium">Set</th>
            <th className="px-3 py-2 text-left font-medium">Set Code</th>
            <th className="px-3 py-2 text-left font-medium">Number</th>
            <th className="px-3 py-2 text-left font-medium">Rarity</th>
            <th className="px-3 py-2 text-left font-medium">Type</th>
            <th className="px-3 py-2 text-left font-medium">Aspects</th>
            <th className="px-3 py-2 text-left font-medium">Release date</th>
          </tr>
        </thead>
        <tbody>
          {shouldVirtualize ? (
            <>
              {paddingTop > 0 && (
                <tr aria-hidden="true">
                  <td colSpan={9} style={{ height: paddingTop, padding: 0 }} />
                </tr>
              )}
              {virtualItems.map((virtualRow) => {
                const printing = printings[virtualRow.index];
                return (
                  <PrintingTableRow
                    key={printing.printingId}
                    printing={printing}
                    onRowClick={onRowClick}
                  />
                );
              })}
              {paddingBottom > 0 && (
                <tr aria-hidden="true">
                  <td colSpan={9} style={{ height: paddingBottom, padding: 0 }} />
                </tr>
              )}
            </>
          ) : (
            printings.map((p) => (
              <PrintingTableRow key={p.printingId} printing={p} onRowClick={onRowClick} />
            ))
          )}
        </tbody>
      </table>
    </div>
  );
}
