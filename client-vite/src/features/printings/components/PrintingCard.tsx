import type { PrintingListItem } from "@/features/printings/api/printings";

type Props = { p: PrintingListItem; onClick?: (p: PrintingListItem) => void };

export function PrintingCard({ p, onClick }: Props) {
  const ariaLabel = `${p.cardName} — ${p.setName}${p.number ? ` #${p.number}` : ""}`;
  
  return (
    <button
      onClick={() => onClick?.(p)}
      className="group w-full bg-transparent text-left p-0 transition focus:ring-2 focus:ring-primary focus:ring-offset-2"
      aria-label={ariaLabel}
    >
      <div className="aspect-[63/88] w-full bg-muted">
        {p.imageUrl ? (
          <img
            src={p.imageUrl}
            alt=""
            className="h-full w-full object-contain transition-transform duration-200 group-hover:scale-[1.02]"
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
