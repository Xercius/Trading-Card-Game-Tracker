// client-vite/src/features/cards/api/list.ts
import http from "@/lib/http";
import type { CardSummary } from "@/components/CardTile";
import type { CardAspect } from "./types";

export type CardsPageParams = {
  q?: string;
  games?: string[];
  sets?: string[];
  rarities?: string[];
  types?: string[];
  arenas?: string[];
  aspects?: string[];
  aspectMethod?: "or" | "and" | "exclude";
  traits?: string[];
  traitMethod?: "or" | "and" | "exclude";
  keywords?: string[];
  keywordMethod?: "or" | "and" | "exclude";
  skip: number;
  take: number;
};

type RawPrimaryCamel = {
  id?: number | string;
  imageUrl?: string | null;
  set?: string | null;
  setCode?: string | null;
  setLogoUrl?: string | null;
  number?: string | null;
  rarity?: string | null;
  releaseDate?: string | null;
} | null;

type RawPrimaryPascal = {
  Id?: number | string;
  ImageUrl?: string | null;
  Set?: string | null;
  SetCode?: string | null;
  SetLogoUrl?: string | null;
  Number?: string | null;
  Rarity?: string | null;
  ReleaseDate?: string | null;
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
  cardType?: string;
  CardType?: string;
  cardTypes?: string[];
  CardTypes?: string[];
  aspects?: CardAspect[];
  Aspects?: CardAspect[];

  primary?: RawPrimaryCamel;
  Primary?: RawPrimaryPascal;

  imageUrl?: string | null;
  ImageUrl?: string | null;
  setName?: string | null;
  SetName?: string | null;
  setCode?: string | null;
  SetCode?: string | null;
  number?: string | null;
  Number?: string | null;
  rarity?: string | null;
  Rarity?: string | null;
  releaseDate?: string | null;
  ReleaseDate?: string | null;
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
  types,
  arenas,
  aspects,
  aspectMethod,
  traits,
  traitMethod,
  keywords,
  keywordMethod,
  skip,
  take,
}: CardsPageParams): Promise<CardsPage> {
  const params = new URLSearchParams();
  if (q) params.set("q", q);
  if (games?.length) params.set("game", games.join(","));
  if (sets?.length) params.set("set", sets.join(","));
  if (rarities?.length) params.set("rarity", rarities.join(","));
  if (types?.length) params.set("type", types.join(","));
  if (arenas?.length) params.set("arena", arenas.join(","));
  if (aspects?.length) params.set("aspect", aspects.join(","));
  if (aspectMethod && aspectMethod !== "or") params.set("aspectMethod", aspectMethod);
  if (traits?.length) params.set("trait", traits.join(","));
  if (traitMethod && traitMethod !== "or") params.set("traitMethod", traitMethod);
  if (keywords?.length) params.set("keyword", keywords.join(","));
  if (keywordMethod && keywordMethod !== "or") params.set("keywordMethod", keywordMethod);
  params.set("skip", String(skip));
  params.set("take", String(take));

  const res = await http.get<RawCardsResponse>("cards", { params });
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
      cardType: item.cardType ?? item.CardType ?? null,
      cardTypes: item.cardTypes ?? item.CardTypes ?? [],
      aspects: item.aspects ?? item.Aspects ?? [],
      imageUrl:
        primaryCamel?.imageUrl ?? primaryPascal?.ImageUrl ?? item.imageUrl ?? item.ImageUrl ?? null,
      setName: primaryCamel?.set ?? primaryPascal?.Set ?? item.setName ?? item.SetName ?? null,
      setCode:
        primaryCamel?.setCode ?? primaryPascal?.SetCode ?? item.setCode ?? item.SetCode ?? null,
      setLogoUrl: primaryCamel?.setLogoUrl ?? primaryPascal?.SetLogoUrl ?? null,
      number: primaryCamel?.number ?? primaryPascal?.Number ?? item.number ?? item.Number ?? null,
      rarity: primaryCamel?.rarity ?? primaryPascal?.Rarity ?? item.rarity ?? item.Rarity ?? null,
      releaseDate:
        primaryCamel?.releaseDate ??
        primaryPascal?.ReleaseDate ??
        item.releaseDate ??
        item.ReleaseDate ??
        null,
    };
  });

  const total = data.total ?? data.Total ?? 0;
  const nextSkip =
    data.nextSkip ?? data.NextSkip ?? (items.length < take ? null : skip + items.length);

  return { items, total, nextSkip };
}

export type FacetOption = {
  value: string;
  label?: string;
  count: number;
};

export type CardsFacetResponse = {
  games: FacetOption[];
  sets: FacetOption[];
  rarities: FacetOption[];
  types: FacetOption[];
  arenas?: FacetOption[];
  aspects?: FacetOption[];
  traits?: FacetOption[];
  keywords?: FacetOption[];
  personas?: FacetOption[];
  treatments?: FacetOption[];
  subTypes?: FacetOption[];
  destinies?: FacetOption[];
  deploys?: FacetOption[];
  forfeits?: FacetOption[];
  powers?: FacetOption[];
  abilities?: FacetOption[];
  lightSideIcons?: FacetOption[];
  darkSideIcons?: FacetOption[];
  stages?: FacetOption[];
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

export async function fetchCardGames(): Promise<string[]> {
  const res = await http.get<ReadonlyArray<string | null | undefined>>("cards/facets/games");
  return normalizeFacetList(res.data ?? []);
}

export async function fetchCardFacets({
  games,
  sets,
  rarities,
  types,
  arenas,
  aspects,
  aspectMethod,
  traits,
  traitMethod,
  keywords,
  keywordMethod,
  side,
  personas,
  subTypes,
  destinies,
  deploys,
  forfeits,
  powers,
  abilities,
  lightSideIcons,
  darkSideIcons,
  hp,
  stage,
  q,
  printingSearch,
}: {
  games?: string[];
  sets?: string[];
  rarities?: string[];
  types?: string[];
  arenas?: string[];
  aspects?: string[];
  aspectMethod?: "or" | "and" | "exclude";
  traits?: string[];
  traitMethod?: "or" | "and" | "exclude";
  keywords?: string[];
  keywordMethod?: "or" | "and" | "exclude";
  side?: string;
  personas?: string[];
  subTypes?: string[];
  destinies?: string[];
  deploys?: string[];
  forfeits?: string[];
  powers?: string[];
  abilities?: string[];
  lightSideIcons?: string[];
  darkSideIcons?: string[];
  hp?: string;
  stage?: string[];
  q?: string;
  printingSearch?: boolean;
}): Promise<CardsFacetResponse> {
  const params = new URLSearchParams();
  if (games?.length) params.set("game", games.join(","));
  if (sets?.length) params.set("set", sets.join(","));
  if (rarities?.length) params.set("rarity", rarities.join(","));
  if (types?.length) params.set("type", types.join(","));
  if (arenas?.length) params.set("arena", arenas.join(","));
  if (aspects?.length) params.set("aspect", aspects.join(","));
  if (aspectMethod && aspectMethod !== "or") params.set("aspectMethod", aspectMethod);
  if (traits?.length) params.set("trait", traits.join(","));
  if (traitMethod && traitMethod !== "or") params.set("traitMethod", traitMethod);
  if (keywords?.length) params.set("keyword", keywords.join(","));
  if (keywordMethod && keywordMethod !== "or") params.set("keywordMethod", keywordMethod);
  if (side) params.set("side", side);
  if (personas?.length) params.set("persona", personas.join(","));
  if (subTypes?.length) params.set("subType", subTypes.join(","));
  if (destinies?.length) params.set("destiny", destinies.join(","));
  if (deploys?.length) params.set("deploy", deploys.join(","));
  if (forfeits?.length) params.set("forfeit", forfeits.join(","));
  if (powers?.length) params.set("power", powers.join(","));
  if (abilities?.length) params.set("ability", abilities.join(","));
  if (lightSideIcons?.length) params.set("lightSideIcon", lightSideIcons.join(","));
  if (darkSideIcons?.length) params.set("darkSideIcon", darkSideIcons.join(","));
  if (hp) params.set("hp", hp);
  if (stage?.length) params.set("stage", stage.join(","));
  if (q) params.set("q", q);
  if (printingSearch) params.set("printingSearch", "true");

  const res = await http.get<CardsFacetResponse>("cards/facets", { params });
  return (
    res.data ?? {
      games: [],
      sets: [],
      rarities: [],
      types: [],
      arenas: [],
      aspects: [],
      traits: [],
      keywords: [],
    }
  );
}
