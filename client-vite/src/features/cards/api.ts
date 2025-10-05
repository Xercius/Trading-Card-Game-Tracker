// client-vite/src/features/cards/api.ts
import { api } from "@/lib/api"; // <-- shared axios instance with X-User-Id interceptor
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

  // Use shared client to retain X-User-Id header propagation
  const res = await api.get(`/api/cards?${params.toString()}`);

  const rawItems: any[] = res.data?.items ?? [];
  const items: CardSummary[] = rawItems.map((item) => ({
    id: item.cardId ?? item.id ?? item.Id ?? "",
    name: item.name,
    game: item.game,
    imageUrl: item.primary?.imageUrl ?? item.imageUrl ?? null,
    setName: item.primary?.set ?? item.setName ?? null,
    number: item.primary?.number ?? item.number ?? null,
    rarity: item.primary?.rarity ?? item.rarity ?? null,
  }));

  return {
    items,
    total: res.data?.total ?? 0,
    nextSkip: res.data?.nextSkip ?? (items.length < take ? null : skip + items.length),
  };
}
