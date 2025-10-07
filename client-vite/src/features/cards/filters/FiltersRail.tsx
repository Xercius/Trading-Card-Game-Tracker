import { useEffect, useMemo, useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import {
  fetchCardGames,
  fetchCardRarities,
  fetchCardSets,
  type CardFacetRarities,
  type CardFacetSets,
} from "../api";
import { useCardFilters } from "./useCardFilters";

type FiltersRailProps = {
  onClose?: () => void;
};

type ChecklistProps = {
  idPrefix: string;
  title: string;
  options: readonly string[];
  selected: readonly string[];
  onToggle: (value: string) => void;
  loading?: boolean;
};

function Checklist({ idPrefix, title, options, selected, onToggle, loading = false }: ChecklistProps) {
  if (loading) {
    return (
      <section aria-label={title} className="space-y-2">
        <h3 className="text-sm font-semibold uppercase tracking-wide text-muted-foreground">{title}</h3>
        <p className="text-sm text-muted-foreground">Loading…</p>
      </section>
    );
  }

  if (options.length === 0) {
    return (
      <section aria-label={title} className="space-y-2">
        <h3 className="text-sm font-semibold uppercase tracking-wide text-muted-foreground">{title}</h3>
        <p className="text-sm text-muted-foreground">No options available</p>
      </section>
    );
  }

  return (
    <section aria-label={title} className="space-y-2">
      <h3 className="text-sm font-semibold uppercase tracking-wide text-muted-foreground">{title}</h3>
      <div className="space-y-2">
        {options.map((option) => {
          const id = `${idPrefix}-${option.replace(/\s+/g, "-").toLowerCase()}`;
          const isChecked = selected.includes(option);
          return (
            <label key={option} htmlFor={id} className="flex cursor-pointer items-center gap-2 text-sm">
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

export default function FiltersRail({ onClose }: FiltersRailProps) {
  const { filters, setFilters, clearAll } = useCardFilters();
  const [searchText, setSearchText] = useState(filters.q);

  useEffect(() => {
    setSearchText(filters.q);
  }, [filters.q]);

  useEffect(() => {
    const handler = window.setTimeout(() => {
      setFilters((prev) => (prev.q === searchText.trim() ? prev : { ...prev, q: searchText.trim() }));
    }, 300);
    return () => window.clearTimeout(handler);
  }, [searchText, setFilters]);

  const gamesQuery = useQuery({
    queryKey: ["card-facets", "games"],
    queryFn: fetchCardGames,
    staleTime: 5 * 60_000,
  });

  const setsQuery = useQuery<CardFacetSets>({
    queryKey: ["card-facets", "sets", filters.games.join("|")],
    queryFn: () => fetchCardSets({ games: filters.games }),
    staleTime: 5 * 60_000,
  });

  const raritiesQuery = useQuery<CardFacetRarities>({
    queryKey: ["card-facets", "rarities", filters.games.join("|")],
    queryFn: () => fetchCardRarities({ games: filters.games }),
    staleTime: 5 * 60_000,
  });

  const gameOptions = gamesQuery.data ?? [];
  const setOptions = setsQuery.data?.sets ?? [];
  const rarityOptions = raritiesQuery.data?.rarities ?? [];

  useEffect(() => {
    if (!setsQuery.data) return;
    const allowed = new Set(setsQuery.data.sets);
    const filtered = filters.sets.filter((value) => allowed.has(value));
    if (filtered.length !== filters.sets.length) {
      setFilters((prev) => ({ ...prev, sets: filtered }));
    }
  }, [filters.sets, setFilters, setsQuery.data]);

  useEffect(() => {
    if (!raritiesQuery.data) return;
    const allowed = new Set(raritiesQuery.data.rarities);
    const filtered = filters.rarities.filter((value) => allowed.has(value));
    if (filtered.length !== filters.rarities.length) {
      setFilters((prev) => ({ ...prev, rarities: filtered }));
    }
  }, [filters.rarities, raritiesQuery.data, setFilters]);

  const handleGameToggle = (value: string) => {
    setFilters((prev) => {
      const hasValue = prev.games.includes(value);
      const nextGames = hasValue ? prev.games.filter((game) => game !== value) : [...prev.games, value];
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
    filters.rarities.length === 0;

  const clearAndClose = () => {
    clearAll();
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
          <label htmlFor="card-filter-search" className="text-sm font-semibold uppercase tracking-wide text-muted-foreground">
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
          loading={gamesQuery.isLoading}
        />

        <Checklist
          idPrefix="card-sets"
          title="Sets"
          options={setOptions}
          selected={filters.sets}
          onToggle={handleSetToggle}
          loading={setsQuery.isLoading}
        />

        <Checklist
          idPrefix="card-rarities"
          title="Rarities"
          options={rarityOptions}
          selected={filters.rarities}
          onToggle={handleRarityToggle}
          loading={raritiesQuery.isLoading}
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
