import type { Dispatch, SetStateAction } from "react";
import type { CardFilters } from "@/features/cards/filters/useCardFilters";
import type { PrintingFacets } from "../../api/usePrintingFacets";
import { MtgColorFilter } from "../MtgColorFilter";
import { MtgColorIdentityFilter } from "../MtgColorIdentityFilter";
import { MultiSelect } from "../MultiSelect";
import { NumericFilterInput } from "./NumericFilterInput";

export interface GameFiltersSectionProps {
  filters: CardFilters;
  setFilters: Dispatch<SetStateAction<CardFilters>>;
  availableFacets: PrintingFacets;
}

/**
 * Renders Magic: The Gathering card filters inside the filter drawer.
 */
export function MtgFiltersSection({
  filters,
  setFilters,
  availableFacets,
}: GameFiltersSectionProps) {
  return (
    <>
      <MultiSelect
        label="Set"
        className="w-full justify-between"
        values={filters.sets ?? []}
        options={availableFacets.sets}
        placeholder="Search sets…"
        onChange={(sets) => setFilters((prev) => ({ ...prev, sets, page: 1 }))}
      />
      <MultiSelect
        label="Type"
        className="w-full justify-between"
        values={filters.types ?? []}
        options={availableFacets.types ?? []}
        placeholder="Search types…"
        onChange={(types) => setFilters((prev) => ({ ...prev, types, page: 1 }))}
      />
      <MultiSelect
        label="Subtype"
        className="w-full justify-between"
        values={filters.subTypes ?? []}
        options={availableFacets.subTypes ?? []}
        placeholder="Search subtypes…"
        onChange={(subTypes) => setFilters((prev) => ({ ...prev, subTypes, page: 1 }))}
      />
      <MultiSelect
        label="Keywords"
        className="w-full justify-between"
        values={filters.keywords ?? []}
        options={availableFacets.keywords ?? []}
        placeholder="Search keywords…"
        onChange={(keywords) =>
          setFilters((prev) => ({ ...prev, keywords, keywordMethod: "or", page: 1 }))
        }
      />
      <MtgColorFilter
        className="w-full justify-between"
        values={filters.aspects ?? []}
        method={filters.aspectMethod ?? "or"}
        availableColors={availableFacets.aspects ?? []}
        onChange={(aspects, aspectMethod) =>
          setFilters((prev) => ({ ...prev, aspects, aspectMethod, page: 1 }))
        }
      />
      <MtgColorIdentityFilter
        className="w-full justify-between"
        values={filters.colorIdentities ?? []}
        availableColors={availableFacets.aspects ?? []}
        onChange={(colorIdentities) =>
          setFilters((prev) => ({ ...prev, colorIdentities, page: 1 }))
        }
      />
      <NumericFilterInput
        id="filter-mtg-mana-value"
        label="Mana Value"
        values={filters.costs}
        placeholder="e.g. >2 or 3"
        onCommit={(costs) => setFilters((prev) => ({ ...prev, costs, page: 1 }))}
      />
      <NumericFilterInput
        id="filter-mtg-power"
        label="Power"
        values={filters.powers}
        placeholder="e.g. >2 or 3"
        onCommit={(powers) => setFilters((prev) => ({ ...prev, powers, page: 1 }))}
      />
      <NumericFilterInput
        id="filter-mtg-toughness"
        label="Toughness"
        values={filters.toughnesses}
        placeholder="e.g. >3 or 4"
        onCommit={(toughnesses) => setFilters((prev) => ({ ...prev, toughnesses, page: 1 }))}
      />
      <NumericFilterInput
        id="filter-mtg-loyalty"
        label="Loyalty"
        values={filters.loyalties}
        placeholder="e.g. >3 or 4"
        onCommit={(loyalties) => setFilters((prev) => ({ ...prev, loyalties, page: 1 }))}
      />
      <MultiSelect
        label="Rarity"
        className="w-full justify-between"
        values={filters.rarities ?? []}
        options={availableFacets.rarities}
        placeholder="Search rarities…"
        onChange={(rarities) => setFilters((prev) => ({ ...prev, rarities, page: 1 }))}
      />
    </>
  );
}
