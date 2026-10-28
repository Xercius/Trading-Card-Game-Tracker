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
      <div className="w-full overflow-hidden bg-muted">
        {p.imageUrl ? (
          <img
            src={p.imageUrl}
            alt=""
            className="block h-auto w-full object-contain transition-transform duration-200 group-hover:scale-[1.02]"
            loading="lazy"
          />
        ) : (
          <div className="aspect-[63/88] grid w-full place-items-center text-xs text-muted-foreground">
            No image
          </div>
        )}
      </div>
    </button>
  );
}
