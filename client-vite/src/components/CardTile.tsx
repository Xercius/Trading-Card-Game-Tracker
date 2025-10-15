import { resolveImageUrl } from "@/lib/http";

export type CardSummary = {
  /**
   * API responses occasionally encode identifiers as strings, so consumers
   * should normalize this value before performing numeric comparisons.
   */
  id: number | string;
  primaryPrintingId: number | null;
  name: string;
  game: string;
  cardType?: string | null;
  setName?: string | null;
  number?: string | null;
  rarity?: string | null;
  imageUrl?: string | null;
};

type Props = {
  card: CardSummary;
  onClick?: (card: CardSummary) => void;
  className?: string;
};

export default function CardTile({ card, onClick, className }: Props) {
  const ariaLabel = `${card.name}${card.setName ? ` — ${card.setName}` : ""}${card.number ? ` #${card.number}` : ""}`;
  
  return (
    <div
      className={`group rounded-2xl border bg-card shadow-sm transition hover:shadow ${className ?? ""}`}
      role="button"
      tabIndex={0}
      onClick={() => onClick?.(card)}
      onKeyDown={(e) => {
        if (e.key === "Enter" || e.key === " ") onClick?.(card);
      }}
      aria-label={ariaLabel}
    >
      <div className="aspect-[63/88] w-full rounded-2xl bg-muted">
        {card.imageUrl ? (
          <img
            loading="lazy"
            decoding="async"
            src={resolveImageUrl(card.imageUrl)} // ← normalize image path here
            alt=""
            className="h-full w-full object-contain"
          />
        ) : (
          <div className="flex h-full w-full items-center justify-center text-xs text-muted-foreground">
            No image
          </div>
        )}
      </div>
    </div>
  );
}
