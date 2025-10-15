import type { PrintingListItem } from "@/features/printings/api/printings";

type Props = { p: PrintingListItem; onClick?: (p: PrintingListItem) => void };

export function PrintingCard({ p, onClick }: Props) {
  const ariaLabel = `${p.cardName} — ${p.setName}${p.number ? ` #${p.number}` : ""}`;
  
  return (
    <button
      onClick={() => onClick?.(p)}
      className="group w-full rounded-md border bg-card text-card-foreground shadow-sm hover:shadow transition p-0 text-left"
      aria-label={ariaLabel}
    >
      <div className="aspect-[3/4] w-full overflow-hidden rounded-md bg-muted">
        {p.imageUrl ? (
          <img
            src={p.imageUrl}
            alt=""
            className="h-full w-full object-cover transition-transform duration-200 group-hover:scale-[1.02]"
            loading="lazy"
          />
        ) : (
          <div className="h-full w-full grid place-items-center text-xs text-muted-foreground">
            No image
          </div>
        )}
      </div>
    </button>
  );
}
