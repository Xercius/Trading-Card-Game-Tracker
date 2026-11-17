import { EvolvesFromFilter } from "../EvolvesFromFilter";
import { MultiSelect } from "../MultiSelect";
import { PokemonTypeFilter } from "../PokemonTypeFilter";
import type { GameFiltersSectionProps } from "./MtgFiltersSection";
import { NumericFilterInput } from "./NumericFilterInput";

const DEFAULT_STAGES = ["Basic", "Stage 1", "Stage 2", "VMAX", "VSTAR", "Restored"];
const DEFAULT_SPECIAL_TYPES = [
  "Pokémon V",
  "Pokémon EX / ex",
  "Mega Evolution",
  "Pokémon-GX",
  "Radiant",
  "Tera",
  "Ancient",
  "Future",
  "ACE SPEC",
];

/**
 * Renders Pokémon TCG card filters inside the filter drawer.
 */
export function PokemonFiltersSection({
  filters,
  setFilters,
  availableFacets,
}: GameFiltersSectionProps) {
  return (
    <>
      <PokemonTypeFilter
        className="w-full justify-between"
        values={filters.aspects ?? []}
        availableTypes={availableFacets.aspects ?? []}
        onChange={(aspects) =>
          setFilters((prev) => ({ ...prev, aspects, aspectMethod: "or", page: 1 }))
        }
      />
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
        label="Stage"
        className="w-full justify-between"
        values={filters.stages ?? []}
        options={
          availableFacets.stages && availableFacets.stages.length > 0
            ? availableFacets.stages
            : DEFAULT_STAGES
        }
        placeholder="Search stages…"
        onChange={(stages) => setFilters((prev) => ({ ...prev, stages, page: 1 }))}
      />
      <MultiSelect
        label="Special Type"
        className="w-full justify-between"
        values={filters.specialTypes ?? []}
        options={
          availableFacets.specialTypes && availableFacets.specialTypes.length > 0
            ? availableFacets.specialTypes
            : DEFAULT_SPECIAL_TYPES
        }
        placeholder="Search special types…"
        onChange={(specialTypes) => setFilters((prev) => ({ ...prev, specialTypes, page: 1 }))}
      />
      <EvolvesFromFilter
        value={filters.evolvesFrom}
        onChange={(evolvesFrom) => setFilters((prev) => ({ ...prev, evolvesFrom, page: 1 }))}
      />
      <NumericFilterInput
        id="filter-pokemon-hp"
        label="HP"
        labelWidthClass="w-12"
        values={filters.hps}
        placeholder="e.g. >109 or 110"
        onCommit={(hps) => setFilters((prev) => ({ ...prev, hps, page: 1 }))}
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
