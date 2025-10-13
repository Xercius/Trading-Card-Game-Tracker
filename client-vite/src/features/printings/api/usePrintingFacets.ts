import type { PrintingListItem } from "./printings";

export type PrintingFacets = {
  games: string[];
  sets: string[];
  rarities: string[];
};

const sortStrings = (values: string[]): string[] =>
  [...values].sort((a, b) => a.localeCompare(b, undefined, { sensitivity: "base" }));

const uniqueStrings = (values: Array<string | null | undefined>): string[] => {
  const seen = new Set<string>();
  values.forEach((value) => {
    const trimmed = value?.trim();
    if (!trimmed) return;
    if (!seen.has(trimmed)) {
      seen.add(trimmed);
    }
  });
  return sortStrings(Array.from(seen));
};

// TODO: Replace derived facets with backend-provided printings facet endpoints when available.
export function useDerivedFacets(printings: PrintingListItem[]): PrintingFacets {
  const games = uniqueStrings(printings.map((printing) => printing.game));
  const sets = uniqueStrings(printings.map((printing) => printing.setName));
  const rarities = uniqueStrings(printings.map((printing) => printing.rarity));
  return { games, sets, rarities };
}
