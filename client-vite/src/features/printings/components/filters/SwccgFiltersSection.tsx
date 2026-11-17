import { Button } from "@/components/ui/button";
import { LightsaberBlade } from "@/features/cards/components/SwccgForceIcons";
import { MultiSelect } from "../MultiSelect";
import type { GameFiltersSectionProps } from "./MtgFiltersSection";
import { NumericFilterInput } from "./NumericFilterInput";

/**
 * Renders Star Wars CCG card filters inside the filter drawer.
 */
export function SwccgFiltersSection({
  filters,
  setFilters,
  availableFacets,
}: GameFiltersSectionProps) {
  const activeSide = filters.side
    ? filters.side.toLowerCase() === "light"
      ? "Light"
      : filters.side.toLowerCase() === "dark"
        ? "Dark"
        : null
    : null;

  return (
    <>
      <div className="space-y-1.5">
        <div className="grid grid-cols-2 gap-2" role="group" aria-label="Side filter">
          <Button
            type="button"
            variant="outline"
            size="sm"
            aria-pressed={activeSide === "Dark"}
            onClick={() =>
              setFilters((prev) => ({
                ...prev,
                side: activeSide === "Dark" ? undefined : "Dark",
                page: 1,
              }))
            }
            className={
              activeSide === "Dark"
                ? "border-red-500 bg-red-950/80 text-red-200 font-bold shadow-sm hover:bg-red-900"
                : "border-white/20 bg-card/40 text-muted-foreground hover:bg-white/10 hover:text-foreground"
            }
          >
            Dark Side
          </Button>
          <Button
            type="button"
            variant="outline"
            size="sm"
            aria-pressed={activeSide === "Light"}
            onClick={() =>
              setFilters((prev) => ({
                ...prev,
                side: activeSide === "Light" ? undefined : "Light",
                page: 1,
              }))
            }
            className={
              activeSide === "Light"
                ? "border-blue-500 bg-blue-950/80 text-blue-200 font-bold shadow-sm hover:bg-blue-900"
                : "border-white/20 bg-card/40 text-muted-foreground hover:bg-white/10 hover:text-foreground"
            }
          >
            Light Side
          </Button>
        </div>
      </div>
      <MultiSelect
        label="Card Type"
        className="w-full justify-between"
        values={filters.types ?? []}
        options={availableFacets.types ?? []}
        placeholder="Search card types…"
        onChange={(types) => setFilters((prev) => ({ ...prev, types, page: 1 }))}
      />
      <MultiSelect
        label="Sub-Type"
        className="w-full justify-between"
        values={filters.subTypes ?? []}
        options={availableFacets.subTypes ?? []}
        placeholder="Search sub-types…"
        onChange={(subTypes) => setFilters((prev) => ({ ...prev, subTypes, page: 1 }))}
      />
      <MultiSelect
        label="Persona"
        className="w-full justify-between"
        values={filters.personas ?? []}
        options={availableFacets.personas ?? []}
        placeholder="Search personas…"
        onChange={(personas) => setFilters((prev) => ({ ...prev, personas, page: 1 }))}
      />
      <MultiSelect
        label="Set"
        className="w-full justify-between"
        values={filters.sets ?? []}
        options={availableFacets.sets ?? []}
        placeholder="Search sets…"
        onChange={(sets) => setFilters((prev) => ({ ...prev, sets, page: 1 }))}
      />
      <MultiSelect
        label="Rarity"
        className="w-full justify-between"
        values={filters.rarities ?? []}
        options={availableFacets.rarities ?? []}
        placeholder="Search rarities…"
        onChange={(rarities) => setFilters((prev) => ({ ...prev, rarities, page: 1 }))}
      />
      <MultiSelect
        label="Traits"
        className="w-full justify-between"
        values={filters.traits ?? []}
        options={availableFacets.traits ?? []}
        placeholder="Search traits…"
        method={filters.traitMethod ?? "or"}
        onMethodChange={(traitMethod) => setFilters((prev) => ({ ...prev, traitMethod, page: 1 }))}
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
        label="Destiny"
        className="w-full justify-between"
        values={filters.destinies ?? []}
        options={availableFacets.destinies ?? []}
        placeholder="Search destinies…"
        onChange={(destinies) => setFilters((prev) => ({ ...prev, destinies, page: 1 }))}
      />
      <MultiSelect
        label="Deploy"
        className="w-full justify-between"
        values={filters.deploys ?? []}
        options={availableFacets.deploys ?? []}
        placeholder="Search deploys…"
        onChange={(deploys) => setFilters((prev) => ({ ...prev, deploys, page: 1 }))}
      />
      <MultiSelect
        label="Forfeit"
        className="w-full justify-between"
        values={filters.forfeits ?? []}
        options={availableFacets.forfeits ?? []}
        placeholder="Search forfeits…"
        onChange={(forfeits) => setFilters((prev) => ({ ...prev, forfeits, page: 1 }))}
      />
      <div className="grid grid-cols-2 gap-2">
        <NumericFilterInput
          id="filter-power"
          label="Power"
          labelWidthClass="w-12"
          values={filters.powers}
          placeholder="e.g. >2"
          onCommit={(powers) => setFilters((prev) => ({ ...prev, powers, page: 1 }))}
        />
        <NumericFilterInput
          id="filter-ability"
          label="Ability"
          labelWidthClass="w-12"
          values={filters.abilities}
          placeholder="e.g. >2"
          onCommit={(abilities) => setFilters((prev) => ({ ...prev, abilities, page: 1 }))}
        />
      </div>
      <div className="grid grid-cols-2 gap-2">
        <div className="flex items-center gap-2" title="Light Side Icons">
          <div className="flex items-center justify-center w-5 shrink-0" aria-hidden="true">
            <LightsaberBlade side="light" size="sm" />
          </div>
          <NumericFilterInput
            id="filter-light-side-icons"
            label="Light Side Icons"
            labelWidthClass="sr-only"
            values={filters.lightSideIcons}
            placeholder="e.g. >1"
            onCommit={(lightSideIcons) =>
              setFilters((prev) => ({ ...prev, lightSideIcons, page: 1 }))
            }
          />
        </div>
        <div className="flex items-center gap-2" title="Dark Side Icons">
          <div className="flex items-center justify-center w-5 shrink-0" aria-hidden="true">
            <LightsaberBlade side="dark" size="sm" />
          </div>
          <NumericFilterInput
            id="filter-dark-side-icons"
            label="Dark Side Icons"
            labelWidthClass="sr-only"
            values={filters.darkSideIcons}
            placeholder="e.g. >1"
            onCommit={(darkSideIcons) =>
              setFilters((prev) => ({ ...prev, darkSideIcons, page: 1 }))
            }
          />
        </div>
      </div>
    </>
  );
}
