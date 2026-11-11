import * as React from "react";
import { Button } from "@/components/ui/button";
import { PokemonEnergyIcon } from "./PokemonEnergyIcon";

export type PokemonTypeFilterProps = {
  values: string[];
  availableTypes?: string[];
  onChange: (values: string[]) => void;
  className?: string;
};

const ORDERED_TYPES = [
  "Grass",
  "Fire",
  "Water",
  "Lightning",
  "Psychic",
  "Fighting",
  "Darkness",
  "Metal",
  "Colorless",
  "Dragon",
  "Fairy",
];

function ChevronDownIcon({ className = "" }: { className?: string }) {
  return (
    <svg
      aria-hidden="true"
      className={`h-4 w-4 ${className}`}
      viewBox="0 0 20 20"
      fill="none"
      stroke="currentColor"
    >
      <path d="M6 8l4 4 4-4" strokeWidth="1.5" strokeLinecap="round" strokeLinejoin="round" />
    </svg>
  );
}

function uniqueValues(values: string[]) {
  const seen = new Set<string>();
  return values
    .map((value) => value.trim())
    .filter((value) => {
      const key = value.toLowerCase();
      if (!key || seen.has(key)) return false;
      seen.add(key);
      return true;
    });
}

export function PokemonTypeFilter({
  values,
  availableTypes,
  onChange,
  className,
}: PokemonTypeFilterProps) {
  const containerRef = React.useRef<HTMLDivElement | null>(null);
  const [open, setOpen] = React.useState(false);
  const selectedTypes = new Set(values.map((value) => value.trim().toLowerCase()));
  const hasNoType = selectedTypes.has("no-type") || selectedTypes.has("no-aspect");
  const selectedNames = [
    ...(hasNoType ? ["No Type"] : []),
    ...ORDERED_TYPES.filter((type) => selectedTypes.has(type.toLowerCase())),
  ];
  const summary = selectedNames.length > 0 ? `Types (${selectedNames.join(", ")})` : "Types";

  const toggleType = (typeName: string) => {
    const typeKey = typeName.toLowerCase();
    const currentValues = uniqueValues(values);
    onChange(
      selectedTypes.has(typeKey)
        ? currentValues.filter((value) => value.toLowerCase() !== typeKey)
        : [...currentValues, typeName]
    );
  };

  const toggleNoType = () => {
    onChange(
      uniqueValues(values)
        .filter((value) => value.toLowerCase() !== "no-type" && value.toLowerCase() !== "no-aspect")
        .concat(hasNoType ? [] : ["no-aspect"])
    );
  };

  React.useEffect(() => {
    if (!open) return;
    const handleClickOutside = (event: MouseEvent) => {
      if (containerRef.current && !containerRef.current.contains(event.target as Node)) {
        setOpen(false);
      }
    };
    const handleKeyDown = (event: KeyboardEvent) => {
      if (event.key === "Escape") setOpen(false);
    };
    document.addEventListener("mousedown", handleClickOutside);
    document.addEventListener("keydown", handleKeyDown);
    return () => {
      document.removeEventListener("mousedown", handleClickOutside);
      document.removeEventListener("keydown", handleKeyDown);
    };
  }, [open]);

  return (
    <div ref={containerRef} className="relative">
      <Button
        variant="outline"
        className={`flex items-center justify-between truncate ${className ?? "min-w-44 max-w-72"}`}
        aria-expanded={open}
        aria-haspopup="dialog"
        onClick={() => setOpen((prev) => !prev)}
      >
        <span className="truncate text-left text-sm font-medium">{summary}</span>
        <ChevronDownIcon
          className={`ml-2 shrink-0 transition-transform ${open ? "-rotate-180" : ""}`}
        />
      </Button>

      {open && (
        <div
          role="dialog"
          aria-label="Filter by Pokémon Type"
          className={`absolute left-0 z-50 mt-2 rounded-lg border border-white/20 bg-[#242424] p-3 shadow-xl ${
            className?.includes("w-full") ? "w-full" : "w-80"
          }`}
        >
          <div className="max-h-64 space-y-1 overflow-y-auto pr-1">
            <div className="mb-1 text-xs font-semibold uppercase tracking-wider text-muted-foreground">
              Types
            </div>
            {ORDERED_TYPES.map((typeName) => {
              const isSelected = selectedTypes.has(typeName.toLowerCase());
              const isAvailable =
                !availableTypes ||
                availableTypes.length === 0 ||
                availableTypes.some((type) => type.trim().toLowerCase() === typeName.toLowerCase());

              return (
                <button
                  key={typeName}
                  type="button"
                  aria-label={typeName}
                  aria-pressed={isSelected}
                  onClick={() => toggleType(typeName)}
                  className={`flex w-full items-center justify-between rounded px-1.5 py-1 text-left transition-colors ${
                    isSelected
                      ? "bg-white/10 font-medium text-white"
                      : "text-foreground/80 hover:bg-white/5"
                  } ${!isAvailable && !isSelected ? "opacity-50" : ""}`}
                >
                  <span className="flex items-center gap-2">
                    <PokemonEnergyIcon type={typeName} size={18} />
                    <span className="text-sm">{typeName}</span>
                  </span>
                  <span aria-hidden="true" className="w-4 text-center text-xs">
                    {isSelected ? "✓" : ""}
                  </span>
                </button>
              );
            })}
            <button
              type="button"
              aria-pressed={hasNoType}
              onClick={toggleNoType}
              className={`flex w-full items-center justify-between rounded border-t border-white/10 px-1.5 py-2 text-left transition-colors ${
                hasNoType
                  ? "bg-white/10 font-medium text-white"
                  : "text-foreground/80 hover:bg-white/5"
              }`}
            >
              <span className="text-sm">Colorless / No Type</span>
              <span aria-hidden="true" className="w-4 text-center text-xs">
                {hasNoType ? "✓" : ""}
              </span>
            </button>
          </div>

          <div className="mt-3 flex items-center justify-between border-t border-white/10 pt-2">
            <button
              type="button"
              onClick={() => onChange([])}
              className="rounded px-2 py-1 text-xs text-muted-foreground transition-colors hover:bg-white/5 hover:text-white"
            >
              Reset
            </button>
            <Button
              size="sm"
              variant="secondary"
              className="h-7 text-xs"
              onClick={() => setOpen(false)}
            >
              Done
            </Button>
          </div>
        </div>
      )}
    </div>
  );
}
