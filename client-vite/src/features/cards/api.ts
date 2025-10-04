import http from "@/lib/http";
import type { CardSummary } from "@/components/CardTile";

// Adjust to your server API. This assumes offset pagination with skip/take.
export type CardsPageParams = {
  q?: string;
  games?: string[]; // e.g., ["Magic","Lorcana"]
  skip: number;
  take: number;
};
export type CardsPage = {
  items: CardSummary[];
  total?: number;
  nextSkip?: number | null;
};

export async function fetchCardsPage({ q, games, skip, take }: CardsPageParams): Promise<CardsPage> {
  const res = await http.get("card", {
    params: {
      ...(q ? { q } : {}),
      ...(games && games.length ? { game: games.join(",") } : {}),
      skip,
      take,
    },
  });

  const rawItems = (res.data.items ?? res.data.results ?? []) as any[];
  const items: CardSummary[] = rawItems.map((r) => ({
    id: String(r.cardId ?? r.id ?? r.cardID ?? r.card_id ?? r.cardid ?? r.Id ?? ""),
    name: r.name,
    game: r.game,
    cardType: r.cardType ?? r.type ?? null,
    imageUrl: r.primary?.imageUrl ?? r.imageUrl ?? r.image_url ?? r.images?.small ?? null,
    setName: r.primary?.set ?? r.setName ?? r.set ?? null,
    number: r.primary?.number ?? r.number ?? r.collectorNumber ?? null,
    rarity: r.primary?.rarity ?? r.rarity ?? null,
  }));

  return {
    items,
    total: res.data.total,
    nextSkip: res.data.nextSkip ?? (items.length < take ? null : skip + take),
  };
}
