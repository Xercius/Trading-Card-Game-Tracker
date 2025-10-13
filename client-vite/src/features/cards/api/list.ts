// client-vite/src/features/cards/api/list.ts
import { useQuery } from "@tanstack/react-query";
import { api } from "@/lib/api";
import type { CardSummary } from "@/components/CardTile";

export type CardsPageParams = {
  q?: string;
  games?: string[];
  sets?: string[];
  rarities?: string[];
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
  sets,
  rarities,
  skip,
  take,
}: CardsPageParams): Promise<CardsPage> {
  const params = new URLSearchParams();
  if (q) params.set("q", q);
  if (games?.length) params.set("game", games.join(","));
  if (sets?.length) params.set("set", sets.join(","));
  if (rarities?.length) params.set("rarity", rarities.join(","));
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
        primaryCamel?.imageUrl ?? primaryPascal?.ImageUrl ?? item.imageUrl ?? item.ImageUrl ?? null,
      setName: primaryCamel?.set ?? primaryPascal?.Set ?? item.setName ?? item.SetName ?? null,
      number: primaryCamel?.number ?? primaryPascal?.Number ?? item.number ?? item.Number ?? null,
      rarity: primaryCamel?.rarity ?? primaryPascal?.Rarity ?? item.rarity ?? item.Rarity ?? null,
    };
  });

  const total = data.total ?? data.Total ?? 0;
  const nextSkip =
    data.nextSkip ?? data.NextSkip ?? (items.length < take ? null : skip + items.length);

  return { items, total, nextSkip };
}

type RawSetsResponse = {
  game?: string | null;
  Game?: string | null;
  sets?: ReadonlyArray<string | null | undefined>;
  Sets?: ReadonlyArray<string | null | undefined>;
};

type RawRaritiesResponse = {
  game?: string | null;
  Game?: string | null;
  rarities?: ReadonlyArray<string | null | undefined>;
  Rarities?: ReadonlyArray<string | null | undefined>;
};

export type CardFacetSets = {
  game?: string;
  sets: string[];
};

export type CardFacetRarities = {
  game?: string;
  rarities: string[];
};

function normalizeFacetList(values?: ReadonlyArray<string | null | undefined>): string[] {
  if (!values) return [];
  const seen = new Set<string>();
  const result: string[] = [];
  for (const value of values) {
    if (!value) continue;
    const trimmed = value.trim();
    if (!trimmed || seen.has(trimmed)) continue;
    seen.add(trimmed);
    result.push(trimmed);
  }
  result.sort((a, b) => a.localeCompare(b));
  return result;
}

function normalizeFacetGame(raw?: string | null): string | undefined {
  const value = raw?.trim();
  return value ? value : undefined;
}

// Printing shape from /api/cards/printings
export type PrintingDto = {
  printingId: number;
  setName: string;
  setCode?: string | null;
  number: string;
  rarity: string;
  imageUrl: string;
  cardId: number;
  cardName: string;
  game: string;
};

export type PrintingsQuery = {
  game?: string;
  set?: string;
  rarity?: string;
  style?: string;
  q?: string;
  page?: number;
  pageSize?: number;
};

export async function listPrintings(q: PrintingsQuery = {}): Promise<PrintingDto[]> {
  const params = new URLSearchParams();
  if (q.game) params.set("game", q.game);
  if (q.set) params.set("set", q.set);
  if (q.rarity) params.set("rarity", q.rarity);
  if (q.style) params.set("style", q.style);
  if (q.q) params.set("q", q.q);
  if (q.page) params.set("page", String(q.page));
  if (q.pageSize) params.set("pageSize", String(q.pageSize));

  const res = await api.get<PrintingDto[]>("cards/printings", { params });
  return res.data ?? [];
}

export function usePrintings(q: PrintingsQuery) {
  return useQuery({
    queryKey: ["cards", "printings", q],
    queryFn: () => listPrintings(q),
    staleTime: 60_000,
    keepPreviousData: true,
  });
}

export async function fetchCardGames(): Promise<string[]> {
  const res = await api.get<ReadonlyArray<string | null | undefined>>("cards/facets/games");
  return normalizeFacetList(res.data ?? []);
}

export async function fetchCardSets({ games }: { games: string[] }): Promise<CardFacetSets> {
  const params = new URLSearchParams();
  if (games.length > 0) params.set("game", games.join(","));
  const res = await api.get<RawSetsResponse>("cards/facets/sets", { params });
  const data = res.data ?? {};
  const sets = normalizeFacetList(data.sets ?? data.Sets);
  const game = normalizeFacetGame(data.game ?? data.Game);
  return { game, sets };
}

export async function fetchCardRarities({
  games,
}: {
  games: string[];
}): Promise<CardFacetRarities> {
  const params = new URLSearchParams();
  if (games.length > 0) params.set("game", games.join(","));
  const res = await api.get<RawRaritiesResponse>("cards/facets/rarities", { params });
  const data = res.data ?? {};
  const rarities = normalizeFacetList(data.rarities ?? data.Rarities);
  const game = normalizeFacetGame(data.game ?? data.Game);
  return { game, rarities };
}
