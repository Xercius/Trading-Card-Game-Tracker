import { AspectFilter } from "../AspectFilter";
import { MultiSelect } from "../MultiSelect";
import type { GameFiltersSectionProps } from "./MtgFiltersSection";
import { NumericFilterInput } from "./NumericFilterInput";

/**
 * Renders Star Wars: Unlimited card filters inside the filter drawer.
 */
export function SwuFiltersSection({
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
      <AspectFilter
        className="w-full justify-between"
        values={filters.aspects ?? []}
        method={filters.aspectMethod ?? "or"}
        availableAspects={availableFacets.aspects ?? []}
        onChange={(aspects, aspectMethod) =>
          setFilters((prev) => ({ ...prev, aspects, aspectMethod, page: 1 }))
        }
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
        label="Arena"
        className="w-full justify-between"
        values={filters.arenas ?? []}
        options={availableFacets.arenas ?? []}
        placeholder="Search arenas…"
        onChange={(arenas) => setFilters((prev) => ({ ...prev, arenas, page: 1 }))}
      />
      <MultiSelect
        label="Trait"
        className="w-full justify-between"
        values={filters.traits ?? []}
        options={availableFacets.traits ?? []}
        placeholder="Search traits…"
        method={filters.traitMethod ?? "or"}
        onMethodChange={(traitMethod) =>
          setFilters((prev) => ({ ...prev, traitMethod, page: 1 }))
        }
        onChange={(traits) =>
          setFilters((prev) => ({
            ...prev,
            traits,
            ...(traits.length === 0 ? { traitMethod: "or" as const } : {}),
            page: 1,
          }))
        }
      />
      <MultiSelect
        label="Keyword"
        className="w-full justify-between"
        values={filters.keywords ?? []}
        options={availableFacets.keywords ?? []}
        placeholder="Search keywords…"
        onChange={(keywords) =>
          setFilters((prev) => ({ ...prev, keywords, keywordMethod: "or", page: 1 }))
        }
      />
      <NumericFilterInput
        id="filter-swu-cost"
        label="Cost"
        values={filters.costs}
        placeholder="e.g. >2 or 3"
        onCommit={(costs) => setFilters((prev) => ({ ...prev, costs, page: 1 }))}
      />
      <NumericFilterInput
        id="filter-swu-power"
        label="Power"
        values={filters.powers}
        placeholder="e.g. >3 or 4"
        onCommit={(powers) => setFilters((prev) => ({ ...prev, powers, page: 1 }))}
      />
      <NumericFilterInput
        id="filter-swu-hp"
        label="HP"
        values={filters.hps}
        placeholder="e.g. >4 or 5"
        onCommit={(hps) => setFilters((prev) => ({ ...prev, hps, page: 1 }))}
      />
      <NumericFilterInput
        id="filter-swu-upgrade-power"
        label="Upgrade Power"
        values={filters.upgradePowers}
        placeholder="e.g. >1 or 2"
        onCommit={(upgradePowers) => setFilters((prev) => ({ ...prev, upgradePowers, page: 1 }))}
      />
      <NumericFilterInput
        id="filter-swu-upgrade-hp"
        label="Upgrade HP"
        values={filters.upgradeHps}
        placeholder="e.g. >1 or 2"
        onCommit={(upgradeHps) => setFilters((prev) => ({ ...prev, upgradeHps, page: 1 }))}
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
