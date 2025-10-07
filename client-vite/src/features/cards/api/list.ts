// client-vite/src/features/cards/api/list.ts
import { api } from "@/lib/api";
import type { CardSummary } from "@/components/CardTile";

export type CardsPageParams = {
  q?: string;
  games?: string[];
  skip: number;
  take: number;
};

type RawPrimaryCamel = {
  id?: number | string;
  imageUrl?: string | null;
  set?: string | null;
  number?: string | null;
  rarity?: string | null;
} | null;

type RawPrimaryPascal = {
  Id?: number | string;
  ImageUrl?: string | null;
  Set?: string | null;
  Number?: string | null;
  Rarity?: string | null;
} | null;

type RawCard = {
  cardId?: number | string;
  CardId?: number | string;
  id?: number | string;
  Id?: number | string;
  name?: string;
  Name?: string;
  game?: string;
  Game?: string;

  primary?: RawPrimaryCamel;
  Primary?: RawPrimaryPascal;

  imageUrl?: string | null;
  ImageUrl?: string | null;
  setName?: string | null;
  SetName?: string | null;
  number?: string | null;
  Number?: string | null;
  rarity?: string | null;
  Rarity?: string | null;
};

type RawCardsResponse = {
  items?: ReadonlyArray<RawCard>;
  Items?: ReadonlyArray<RawCard>;
  total?: number;
  Total?: number;
  nextSkip?: number | null;
  NextSkip?: number | null;
};

export type CardsPage = {
  items: CardSummary[];
  total: number;
  nextSkip: number | null;
};

export async function fetchCardsPage({
  q,
  games,
  skip,
  take,
}: CardsPageParams): Promise<CardsPage> {
  const params = new URLSearchParams();
  if (q) params.set("q", q);
  if (games?.length) params.set("game", games.join(","));
  params.set("skip", String(skip));
  params.set("take", String(take));

  const res = await api.get<RawCardsResponse>("cards", { params });
  const data: RawCardsResponse = res.data ?? {};

  const rawItems: ReadonlyArray<RawCard> = data.items ?? data.Items ?? [];
  const items: CardSummary[] = rawItems.map((item) => {
    const primaryCamel = item.primary ?? null;
    const primaryPascal = item.Primary ?? null;

    const rawPrimaryId = primaryCamel?.id ?? primaryPascal?.Id ?? null;
    let primaryPrintingId: number | null = null;
    if (rawPrimaryId != null) {
      const parsed = Number(rawPrimaryId);
      primaryPrintingId = Number.isFinite(parsed) ? parsed : null;
    }

    return {
      id: item.cardId ?? item.CardId ?? item.id ?? item.Id ?? "",
      primaryPrintingId,
      name: item.name ?? item.Name ?? "",
      game: item.game ?? item.Game ?? "",
      imageUrl:
        primaryCamel?.imageUrl ??
        primaryPascal?.ImageUrl ??
        item.imageUrl ??
        item.ImageUrl ??
        null,
      setName:
        primaryCamel?.set ??
        primaryPascal?.Set ??
        item.setName ??
        item.SetName ??
        null,
      number:
        primaryCamel?.number ??
        primaryPascal?.Number ??
        item.number ??
        item.Number ??
        null,
      rarity:
        primaryCamel?.rarity ??
        primaryPascal?.Rarity ??
        item.rarity ??
        item.Rarity ??
        null,
    };
  });

  const total = data.total ?? data.Total ?? 0;
  const nextSkip =
    data.nextSkip ?? data.NextSkip ?? (items.length < take ? null : skip + items.length);

  return { items, total, nextSkip };
}
