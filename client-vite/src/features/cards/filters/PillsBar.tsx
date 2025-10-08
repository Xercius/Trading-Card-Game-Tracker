import { Button } from "@/components/ui/button";
import { useCardFilters } from "./useCardFilters";

type PillToken = {
  key: string;
  label: string;
  ariaLabel: string;
  onRemove: () => void;
};

export default function PillsBar() {
  const { filters, setFilters, clearAll } = useCardFilters();

  const tokens: PillToken[] = [];

  if (filters.q.trim().length > 0) {
    const label = `Search: ${filters.q}`;
    tokens.push({
      key: `q:${filters.q}`,
      label,
      ariaLabel: `Remove search filter ${filters.q}`,
      onRemove: () => setFilters((prev) => ({ ...prev, q: "" })),
    });
  }

  filters.games.forEach((game) => {
    tokens.push({
      key: `game:${game}`,
      label: `Game: ${game}`,
      ariaLabel: `Remove game filter ${game}`,
      onRemove: () =>
        setFilters((prev) => ({ ...prev, games: prev.games.filter((g) => g !== game) })),
    });
  });

  filters.sets.forEach((set) => {
    tokens.push({
      key: `set:${set}`,
      label: `Set: ${set}`,
      ariaLabel: `Remove set filter ${set}`,
      onRemove: () =>
        setFilters((prev) => ({ ...prev, sets: prev.sets.filter((value) => value !== set) })),
    });
  });

  filters.rarities.forEach((rarity) => {
    tokens.push({
      key: `rarity:${rarity}`,
      label: `Rarity: ${rarity}`,
      ariaLabel: `Remove rarity filter ${rarity}`,
      onRemove: () =>
        setFilters((prev) => ({
          ...prev,
          rarities: prev.rarities.filter((value) => value !== rarity),
        })),
    });
  });

  if (tokens.length === 0) {
    return null;
  }

  return (
    <div className="mb-3 flex flex-wrap items-center justify-between gap-2 rounded-md border bg-card px-3 py-2">
      <div className="flex flex-wrap items-center gap-2">
        {tokens.map((token) => (
          <span
            key={token.key}
            className="flex items-center gap-2 rounded-full border border-input bg-background px-3 py-1 text-sm"
          >
            <span>{token.label}</span>
            <button
              type="button"
              onClick={token.onRemove}
              aria-label={token.ariaLabel}
              className="inline-flex h-5 w-5 items-center justify-center rounded-full border border-transparent text-xs font-semibold text-muted-foreground transition-colors hover:bg-muted focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2"
            >
              ×
            </button>
          </span>
        ))}
      </div>
      <Button variant="ghost" size="sm" onClick={clearAll}>
        Clear all
      </Button>
    </div>
  );
}
