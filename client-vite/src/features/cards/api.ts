import axios from "axios";
import type { CardSummary } from "@/components/CardTile";

export type CardsPageParams = {
  q?: string;
  games?: string[];
  skip: number;
  take: number;
};
export type CardsPage = {
  items: CardSummary[];
  total: number;
  nextSkip: number | null;
};

export async function fetchCardsPage({ q, games, skip, take }: CardsPageParams): Promise<CardsPage> {
  const params = new URLSearchParams();
  if (q) params.set("q", q);
  if (games?.length) params.set("game", games.join(","));
  params.set("skip", String(skip));
  params.set("take", String(take));

  const res = await axios.get(`/api/cards?${params.toString()}`);

  const rawItems: any[] = res.data.items ?? [];
  const items: CardSummary[] = rawItems.map((item) => ({
    id: item.cardId ?? item.id ?? item.cardID ?? item.card_id ?? item.cardid ?? item.Id ?? "",
    name: item.name,
    game: item.game,
    cardType: item.cardType ?? item.type ?? null,
    imageUrl: item.primary?.imageUrl ?? item.imageUrl ?? item.images?.small ?? null,
    setName: item.primary?.set ?? item.setName ?? item.set ?? null,
    number: item.primary?.number ?? item.number ?? item.collectorNumber ?? null,
    rarity: item.primary?.rarity ?? item.rarity ?? null,
  }));

  return {
    items,
    total: res.data.total ?? 0,
    nextSkip: res.data.nextSkip ?? (items.length < take ? null : skip + items.length),
  };
}
