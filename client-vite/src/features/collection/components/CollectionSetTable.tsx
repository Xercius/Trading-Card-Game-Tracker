import { useRef } from "react";
import { useVirtualizer } from "@tanstack/react-virtual";
import { CardImageHoverPreview } from "@/components/ui/CardImageHoverPreview";
import { QuantityStepper } from "./QuantityStepper";

export interface SetPrintingRow {
  printingId: number;
  cardId: number;
  cardName: string;
  number: string;
  numericNumber: number;
  treatment: string;
  rarity: string;
  imageUrl: string | null;
  isHorizontal: boolean;
  owned: number;
  proxy: number;
  wanted: number;
}

type CollectionSetTableProps = {
  rows: SetPrintingRow[];
  ready: boolean;
  onUpdateQty: (row: SetPrintingRow, field: "owned" | "proxy" | "wanted", nextVal: number) => void;
  onEditRow: (row: SetPrintingRow) => void;
};

const VIRTUALIZATION_THRESHOLD = 50;
const ESTIMATED_ROW_HEIGHT = 60;

/**
 * Displays set printings with non-blocking quantity steppers and table virtualization
 * for large sets (>50 rows).
 */
export function CollectionSetTable({
  rows,
  ready,
  onUpdateQty,
  onEditRow,
}: CollectionSetTableProps) {
  const scrollRef = useRef<HTMLDivElement | null>(null);
  const shouldVirtualize = rows.length > VIRTUALIZATION_THRESHOLD;

  const virtualizer = useVirtualizer({
    count: shouldVirtualize ? rows.length : 0,
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

  const renderRow = (row: SetPrintingRow) => (
    <tr
      key={row.printingId}
      onClick={() => onEditRow(row)}
      className="cursor-pointer bg-zinc-900 hover:bg-zinc-800/60 transition-colors group"
    >
      <td className="px-2 py-2 text-center" onClick={(e) => e.stopPropagation()}>
        <div className="flex items-center justify-center w-12 h-11 mx-auto">
          <CardImageHoverPreview
            imageUrl={row.imageUrl}
            cardName={row.cardName}
            isHorizontal={row.isHorizontal}
          />
        </div>
      </td>
      <td className="px-3 py-2 text-center font-mono text-xs font-semibold text-zinc-400">
        {row.number}
      </td>
      <td className="px-4 py-2 font-medium text-zinc-100 group-hover:text-purple-400 transition-colors">
        {row.cardName}
      </td>
      <td className="px-3 py-2 text-xs">
        <span className="inline-block rounded bg-zinc-800 border border-zinc-700/60 px-2 py-0.5 text-zinc-300 font-medium">
          {row.treatment}
        </span>
      </td>
      <td className="px-3 py-2 text-xs text-zinc-400 font-medium">{row.rarity}</td>
      <td className="px-2 py-2 text-center" onClick={(e) => e.stopPropagation()}>
        <QuantityStepper
          value={row.owned}
          disabled={!ready}
          ariaLabel={`${row.cardName} owned quantity`}
          onCommit={(nextVal) => onUpdateQty(row, "owned", nextVal)}
        />
      </td>
      <td className="px-2 py-2 text-center" onClick={(e) => e.stopPropagation()}>
        <QuantityStepper
          value={row.proxy}
          disabled={!ready}
          textColorClass="text-amber-300"
          ariaLabel={`${row.cardName} proxy quantity`}
          onCommit={(nextVal) => onUpdateQty(row, "proxy", nextVal)}
        />
      </td>
      <td className="px-2 py-2 text-center" onClick={(e) => e.stopPropagation()}>
        <QuantityStepper
          value={row.wanted}
          disabled={!ready}
          textColorClass="text-indigo-300"
          ariaLabel={`${row.cardName} wishlist quantity`}
          onCommit={(nextVal) => onUpdateQty(row, "wanted", nextVal)}
        />
      </td>
      <td className="px-3 py-2 text-center" onClick={(e) => e.stopPropagation()}>
        <button
          type="button"
          onClick={() => onEditRow(row)}
          title="Edit collection entry"
          className="rounded p-1 text-xs text-zinc-400 hover:bg-zinc-700 hover:text-white transition-colors"
        >
          ✎
        </button>
      </td>
    </tr>
  );

  return (
    <div className="overflow-hidden rounded-xl border border-zinc-800 bg-zinc-900 shadow">
      <div
        ref={scrollRef}
        className={shouldVirtualize ? "max-h-[70vh] overflow-auto" : "overflow-x-auto"}
      >
        <table className="w-full text-left text-sm text-zinc-300">
          <thead className="sticky top-0 z-10 border-b border-zinc-800 bg-zinc-950/95 text-xs uppercase tracking-wider text-zinc-400 backdrop-blur-sm">
            <tr>
              <th scope="col" className="w-16 px-2 py-3 text-center font-semibold">
                Image
              </th>
              <th scope="col" className="w-16 px-3 py-3 text-center font-semibold">
                #
              </th>
              <th scope="col" className="px-4 py-3 font-semibold">
                Card Name
              </th>
              <th scope="col" className="w-32 px-3 py-3 font-semibold">
                Treatment
              </th>
              <th scope="col" className="w-24 px-3 py-3 font-semibold">
                Rarity
              </th>
              <th scope="col" className="w-28 px-3 py-3 text-center font-semibold">
                Owned
              </th>
              <th scope="col" className="w-28 px-3 py-3 text-center font-semibold">
                Proxy
              </th>
              <th scope="col" className="w-28 px-3 py-3 text-center font-semibold">
                Wishlist
              </th>
              <th scope="col" className="w-16 px-3 py-3 text-center font-semibold">
                Edit
              </th>
            </tr>
          </thead>
          <tbody className="divide-y divide-zinc-800/60">
            {shouldVirtualize ? (
              <>
                {paddingTop > 0 && (
                  <tr aria-hidden="true">
                    <td colSpan={9} style={{ height: paddingTop, padding: 0 }} />
                  </tr>
                )}
                {virtualItems.map((virtualRow) => renderRow(rows[virtualRow.index]))}
                {paddingBottom > 0 && (
                  <tr aria-hidden="true">
                    <td colSpan={9} style={{ height: paddingBottom, padding: 0 }} />
                  </tr>
                )}
              </>
            ) : (
              rows.map(renderRow)
            )}
          </tbody>
        </table>
      </div>
    </div>
  );
}
