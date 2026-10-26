import type { PrintingListItem } from "@/features/printings/api/printings";

type Props = {
  printings: PrintingListItem[];
  onRowClick?: (p: PrintingListItem) => void;
};

export function PrintingTableView({ printings, onRowClick }: Props) {
  return (
    <div className="overflow-x-auto rounded-md border">
      <table className="w-full text-sm">
        <thead className="bg-muted text-muted-foreground">
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
          {printings.map((p) => (
            <tr
              key={p.printingId}
              className={
                onRowClick
                  ? "border-t cursor-pointer hover:bg-muted/50 transition-colors focus:outline-none focus:bg-muted/70"
                  : "border-t"
              }
              onClick={() => onRowClick?.(p)}
              tabIndex={onRowClick ? 0 : undefined}
              role={onRowClick ? "button" : undefined}
              onKeyDown={
                onRowClick
                  ? (e) => {
                      if (e.key === "Enter" || e.key === " ") {
                        e.preventDefault();
                        onRowClick(p);
                      }
                    }
                  : undefined
              }
            >
              <td className="px-3 py-2">{p.cardName}</td>
              <td className="px-3 py-2">{p.game}</td>
              <td className="px-3 py-2">{p.setName}</td>
              <td className="px-3 py-2">{p.setCode ?? "—"}</td>
              <td className="px-3 py-2">{p.number ?? "—"}</td>
              <td className="px-3 py-2">{p.rarity ?? "—"}</td>
              <td className="px-3 py-2">{p.cardTypes?.join(", ") || "—"}</td>
              <td className="px-3 py-2">
                {p.aspects
                  ?.map(({ name, count }) => `${name}${count > 1 ? ` ×${count}` : ""}`)
                  .join(", ") || "—"}
              </td>
              <td className="px-3 py-2">{p.releaseDate ?? "—"}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
