import LazyImage from "./LazyImage";
import { resolveImageUrl } from "@/lib/http";

export type CardSummary = {
  id: number | string;
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
  return (
    <div
      className={`group rounded-2xl border bg-card shadow-sm transition hover:shadow ${className ?? ""}`}
      role="button"
      tabIndex={0}
      onClick={() => onClick?.(card)}
      onKeyDown={(e) => {
        if (e.key === "Enter" || e.key === " ") onClick?.(card);
      }}
    >
      <div className="aspect-[3/4] w-full overflow-hidden rounded-t-2xl bg-muted">
        {card.imageUrl ? (
          <LazyImage
            src={resolveImageUrl(card.imageUrl)} // ← normalize image path here
            alt={card.name}
            className="h-full w-full"
          />
        ) : (
          <div className="flex h-full w-full items-center justify-center text-xs text-muted-foreground">
            No image
          </div>
        )}
      </div>
      <div className="space-y-0.5 p-3">
        <div className="line-clamp-1 text-sm font-medium">{card.name}</div>
        <div className="line-clamp-1 text-xs text-muted-foreground">
          {card.game}
          {card.setName ? ` • ${card.setName}` : ""}
          {card.number ? ` • #${card.number}` : ""}
        </div>
      </div>
    </div>
  );
}