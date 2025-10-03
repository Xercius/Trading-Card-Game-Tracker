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
  const params = new URLSearchParams();
  if (q) params.set("q", q);
  if (games && games.length) params.set("game", games.join(","));
  params.set("skip", String(skip));
  params.set("take", String(take));

  const res = await http.get(`/api/card?${params.toString()}`);
  const raw = Array.isArray(res.data) ? res.data : (res.data.items ?? res.data.results ?? []);
  const items = (raw as any[]).map((r) => ({
    id: r.id ?? r.cardId ?? r.cardID ?? String(r.name),
    name: r.name,
    game: r.game,
    setName: r.setName ?? r.set ?? null,
    number: r.number ?? r.collectorNumber ?? null,
    rarity: r.rarity ?? null,
    imageUrl: r.imageUrl ?? r.image_url ?? r.images?.small ?? null,
  })) as CardSummary[];

  return {
    items,
    total: res.data.total,
    nextSkip: res.data.nextSkip ?? (items.length < take ? null : skip + take),
  };
}
