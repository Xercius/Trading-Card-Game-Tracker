import { useEffect, useMemo, useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { fetchCardFacets, type CardsFacetResponse } from "../api";
import { useCardFilters } from "./useCardFilters";

type FiltersRailProps = {
  onClose?: () => void;
  onClearAll?: () => void;
};

type ChecklistProps = {
  idPrefix: string;
  title: string;
  options: readonly string[];
  selected: readonly string[];
  onToggle: (value: string) => void;
  loading?: boolean;
};

function Checklist({
  idPrefix,
  title,
  options,
  selected,
  onToggle,
  loading = false,
}: ChecklistProps) {
  if (loading) {
    return (
      <section aria-label={title} className="space-y-2">
        <h3 className="text-sm font-semibold uppercase tracking-wide text-muted-foreground">
          {title}
        </h3>
        <p className="text-sm text-muted-foreground">Loading…</p>
      </section>
    );
  }

  if (options.length === 0) {
    return (
      <section aria-label={title} className="space-y-2">
        <h3 className="text-sm font-semibold uppercase tracking-wide text-muted-foreground">
          {title}
        </h3>
        <p className="text-sm text-muted-foreground">No options available</p>
      </section>
    );
  }

  return (
    <section aria-label={title} className="space-y-2">
      <h3 className="text-sm font-semibold uppercase tracking-wide text-muted-foreground">
        {title}
      </h3>
      <div className="space-y-2">
        {options.map((option) => {
          const id = `${idPrefix}-${option.replace(/\s+/g, "-").toLowerCase()}`;
          const isChecked = selected.includes(option);
          return (
            <label
              key={option}
              htmlFor={id}
              className="flex cursor-pointer items-center gap-2 text-sm"
            >
              <input
                id={id}
                type="checkbox"
                checked={isChecked}
                onChange={() => onToggle(option)}
                className="h-4 w-4 rounded border border-input text-primary focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2"
              />
              <span>{option}</span>
            </label>
          );
        })}
      </div>
    </section>
  );
}

export default function FiltersRail({ onClose, onClearAll }: FiltersRailProps) {
  const { filters, setFilters, clearAll } = useCardFilters();
  const [searchText, setSearchText] = useState(filters.q);

  useEffect(() => {
    setSearchText(filters.q);
  }, [filters.q]);

  useEffect(() => {
    const handler = window.setTimeout(() => {
      setFilters((prev) =>
        prev.q === searchText.trim() ? prev : { ...prev, q: searchText.trim() }
      );
    }, 300);
    return () => window.clearTimeout(handler);
  }, [searchText, setFilters]);

  // Use unified facets endpoint with all active filters
  const facetsQuery = useQuery<CardsFacetResponse>({
    queryKey: [
      "card-facets",
      "unified",
      filters.games.join("|"),
      filters.sets.join("|"),
      filters.rarities.join("|"),
      filters.types.join("|"),
      filters.arenas.join("|"),
      filters.aspects.join("|"),
      filters.traits.join("|"),
      filters.q,
    ],
    queryFn: () =>
      fetchCardFacets({
        games: filters.games,
        sets: filters.sets,
        rarities: filters.rarities,
        types: filters.types,
        arenas: filters.arenas,
        aspects: filters.aspects,
        traits: filters.traits,
        q: filters.q,
      }),
    staleTime: 5 * 60_000,
  });

  const gameOptions = useMemo(
    () => facetsQuery.data?.games.map((f) => f.value) ?? [],
    [facetsQuery.data?.games]
  );
  const setOptions = useMemo(
    () => facetsQuery.data?.sets.map((f) => f.value) ?? [],
    [facetsQuery.data?.sets]
  );
  const rarityOptions = useMemo(
    () => facetsQuery.data?.rarities.map((f) => f.value) ?? [],
    [facetsQuery.data?.rarities]
  );
  const typeOptions = useMemo(
    () => facetsQuery.data?.types.map((f) => f.value) ?? [],
    [facetsQuery.data?.types]
  );
  const arenaOptions = useMemo(
    () => (facetsQuery.data?.arenas ?? []).map((f) => f.value),
    [facetsQuery.data?.arenas]
  );
  const aspectOptions = useMemo(
    () => (facetsQuery.data?.aspects ?? []).map((f) => f.value),
    [facetsQuery.data?.aspects]
  );
  const traitOptions = useMemo(
    () => (facetsQuery.data?.traits ?? []).map((f) => f.value),
    [facetsQuery.data?.traits]
  );

  // Auto-clear invalid selections when facets update
  useEffect(() => {
    if (!facetsQuery.data) return;

    const allowedGames = new Set(gameOptions);
    const allowedSets = new Set(setOptions);
    const allowedRarities = new Set(rarityOptions);
    const allowedTypes = new Set(typeOptions);
    const allowedArenas = new Set(arenaOptions);
    const allowedAspects = new Set(aspectOptions);
    const allowedTraits = new Set(traitOptions);

    const shouldValidateGames = allowedGames.size > 0;
    const shouldValidateSets = allowedSets.size > 0;
    const shouldValidateRarities = allowedRarities.size > 0;

    const hasInvalidGames =
      shouldValidateGames && filters.games.some((value) => !allowedGames.has(value));
    const hasInvalidSets =
      shouldValidateSets && filters.sets.some((value) => !allowedSets.has(value));
    const hasInvalidRarities =
      shouldValidateRarities && filters.rarities.some((value) => !allowedRarities.has(value));
    const hasInvalidTypes =
      allowedTypes.size > 0 && filters.types.some((value) => !allowedTypes.has(value));
    const hasInvalidArenas =
      allowedArenas.size > 0 && filters.arenas.some((value) => !allowedArenas.has(value));
    const hasInvalidAspects =
      allowedAspects.size > 0 && filters.aspects.some((value) => !allowedAspects.has(value));
    const hasInvalidTraits =
      allowedTraits.size > 0 && filters.traits.some((value) => !allowedTraits.has(value));

    if (
      !hasInvalidGames &&
      !hasInvalidSets &&
      !hasInvalidRarities &&
      !hasInvalidTypes &&
      !hasInvalidArenas &&
      !hasInvalidAspects &&
      !hasInvalidTraits
    ) {
      return;
    }

    setFilters((prev) => ({
      ...prev,
      games:
        hasInvalidGames && shouldValidateGames
          ? prev.games.filter((value) => allowedGames.has(value))
          : prev.games,
      sets:
        hasInvalidSets && shouldValidateSets
          ? prev.sets.filter((value) => allowedSets.has(value))
          : prev.sets,
      rarities:
        hasInvalidRarities && shouldValidateRarities
          ? prev.rarities.filter((value) => allowedRarities.has(value))
          : prev.rarities,
      types: hasInvalidTypes ? prev.types.filter((value) => allowedTypes.has(value)) : prev.types,
      arenas: hasInvalidArenas
        ? prev.arenas.filter((value) => allowedArenas.has(value))
        : prev.arenas,
      aspects: hasInvalidAspects
        ? prev.aspects.filter((value) => allowedAspects.has(value))
        : prev.aspects,
      traits: hasInvalidTraits
        ? prev.traits.filter((value) => allowedTraits.has(value))
        : prev.traits,
    }));
  }, [
    filters.games,
    filters.sets,
    filters.rarities,
    filters.types,
    filters.arenas,
    filters.aspects,
    filters.traits,
    setFilters,
    facetsQuery.data,
    gameOptions,
    setOptions,
    rarityOptions,
    typeOptions,
    arenaOptions,
    aspectOptions,
    traitOptions,
  ]);

  const handleGameToggle = (value: string) => {
    setFilters((prev) => {
      const hasValue = prev.games.includes(value);
      const nextGames = hasValue
        ? prev.games.filter((game) => game !== value)
        : [...prev.games, value];
      return { ...prev, games: nextGames };
    });
  };

  const handleSetToggle = (value: string) => {
    setFilters((prev) => {
      const hasValue = prev.sets.includes(value);
      const nextSets = hasValue ? prev.sets.filter((set) => set !== value) : [...prev.sets, value];
      return { ...prev, sets: nextSets };
    });
  };

  const handleRarityToggle = (value: string) => {
    setFilters((prev) => {
      const hasValue = prev.rarities.includes(value);
      const nextRarities = hasValue
        ? prev.rarities.filter((rarity) => rarity !== value)
        : [...prev.rarities, value];
      return { ...prev, rarities: nextRarities };
    });
  };

  const disableClear =
    filters.q.trim().length === 0 &&
    filters.games.length === 0 &&
    filters.sets.length === 0 &&
    filters.rarities.length === 0 &&
    filters.types.length === 0 &&
    filters.arenas.length === 0 &&
    filters.aspects.length === 0 &&
    filters.traits.length === 0;

  const clearAndClose = () => {
    clearAll();
    onClearAll?.();
    onClose?.();
  };

  const closeButton = useMemo(() => {
    if (!onClose) return null;
    return (
      <Button
        variant="ghost"
        size="sm"
        onClick={onClose}
        aria-label="Close filters"
        className="ml-auto"
      >
        Close
      </Button>
    );
  }, [onClose]);

  return (
    <div className="flex h-full flex-col gap-6 p-4">
      <header className="flex items-center gap-2">
        <h2 className="text-lg font-semibold">Filters</h2>
        {closeButton}
      </header>

      <div className="space-y-6 overflow-y-auto pr-1">
        <div className="space-y-2">
          <label
            htmlFor="card-filter-search"
            className="text-sm font-semibold uppercase tracking-wide text-muted-foreground"
          >
            Search
          </label>
          <Input
            id="card-filter-search"
            value={searchText}
            onChange={(event) => setSearchText(event.target.value)}
            placeholder="Search cards"
            aria-label="Search cards"
          />
        </div>

        <Checklist
          idPrefix="card-games"
          title="Games"
          options={gameOptions}
          selected={filters.games}
          onToggle={handleGameToggle}
          loading={facetsQuery.isLoading}
        />

        <Checklist
          idPrefix="card-sets"
          title="Sets"
          options={setOptions}
          selected={filters.sets}
          onToggle={handleSetToggle}
          loading={facetsQuery.isLoading}
        />

        <Checklist
          idPrefix="card-rarities"
          title="Rarities"
          options={rarityOptions}
          selected={filters.rarities}
          onToggle={handleRarityToggle}
          loading={facetsQuery.isLoading}
        />

        <Checklist
          idPrefix="card-types"
          title="Types"
          options={typeOptions}
          selected={filters.types}
          onToggle={(value) =>
            setFilters((prev) => ({
              ...prev,
              types: prev.types.includes(value)
                ? prev.types.filter((type) => type !== value)
                : [...prev.types, value],
            }))
          }
          loading={facetsQuery.isLoading}
        />

        <Checklist
          idPrefix="card-arenas"
          title="Arenas"
          options={arenaOptions}
          selected={filters.arenas}
          onToggle={(value) =>
            setFilters((prev) => ({
              ...prev,
              arenas: prev.arenas.includes(value)
                ? prev.arenas.filter((arena) => arena !== value)
                : [...prev.arenas, value],
            }))
          }
          loading={facetsQuery.isLoading}
        />

        <Checklist
          idPrefix="card-aspects"
          title="Aspects"
          options={aspectOptions}
          selected={filters.aspects}
          onToggle={(value) =>
            setFilters((prev) => ({
              ...prev,
              aspects: prev.aspects.includes(value)
                ? prev.aspects.filter((aspect) => aspect !== value)
                : [...prev.aspects, value],
            }))
          }
          loading={facetsQuery.isLoading}
        />

        <Checklist
          idPrefix="card-traits"
          title="Traits"
          options={traitOptions}
          selected={filters.traits}
          onToggle={(value) =>
            setFilters((prev) => ({
              ...prev,
              traits: prev.traits.includes(value)
                ? prev.traits.filter((trait) => trait !== value)
                : [...prev.traits, value],
            }))
          }
          loading={facetsQuery.isLoading}
        />
      </div>

      <div className="mt-auto flex gap-2">
        <Button variant="outline" size="sm" onClick={clearAndClose} disabled={disableClear}>
          Clear all
        </Button>
      </div>
    </div>
  );
}
