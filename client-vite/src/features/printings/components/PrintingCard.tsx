import type { PrintingListItem } from "@/features/printings/api/printings";

type Props = { p: PrintingListItem; onClick?: (p: PrintingListItem) => void };

export function PrintingCard({ p, onClick }: Props) {
  return (
    <button
      onClick={() => onClick?.(p)}
      className="group w-full rounded-md border bg-card text-card-foreground shadow-sm hover:shadow transition p-3 text-left"
    >
      <div className="aspect-[3/4] w-full overflow-hidden rounded-sm mb-2 bg-muted">
        {p.imageUrl ? (
          <img
            src={p.imageUrl}
            alt={p.cardName}
            className="h-full w-full object-cover transition-transform duration-200 group-hover:scale-[1.02]"
            loading="lazy"
          />
        ) : (
          <div className="h-full w-full grid place-items-center text-xs text-muted-foreground">
            No image
          </div>
        )}
      </div>
      <div className="font-medium leading-tight line-clamp-2">{p.cardName}</div>
      <div className="mt-1 text-xs text-muted-foreground">
        {p.game} • {p.setName}{p.number ? ` #${p.number}` : ""}{p.rarity ? ` • ${p.rarity}` : ""}
      </div>
    </button>
  );
}
