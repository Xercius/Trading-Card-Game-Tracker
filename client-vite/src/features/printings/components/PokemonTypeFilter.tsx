import * as React from "react";
import { Button } from "@/components/ui/button";
import type { AspectMethod } from "@/features/cards/filters/useCardFilters";
import { PokemonEnergyIcon } from "./PokemonEnergyIcon";

export type PokemonTypeFilterProps = {
  values: string[];
  method: AspectMethod;
  availableTypes?: string[];
  onChange: (values: string[], method: AspectMethod) => void;
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

function MinusIcon({ className = "" }: { className?: string }) {
  return (
    <svg
      aria-hidden="true"
      className={`h-3 w-3 ${className}`}
      viewBox="0 0 20 20"
      fill="currentColor"
    >
      <path
        fillRule="evenodd"
        d="M4 10a.75.75 0 01.75-.75h10.5a.75.75 0 010 1.5H4.75A.75.75 0 014 10z"
        clipRule="evenodd"
      />
    </svg>
  );
}

function PlusIcon({ className = "" }: { className?: string }) {
  return (
    <svg
      aria-hidden="true"
      className={`h-3 w-3 ${className}`}
      viewBox="0 0 20 20"
      fill="currentColor"
    >
      <path
        fillRule="evenodd"
        d="M10 4.75a.75.75 0 01.75.75v3.75h3.75a.75.75 0 010 1.5H10.75v3.75a.75.75 0 01-1.5 0V10.75H5.5a.75.75 0 010-1.5h3.75V5.5a.75.75 0 01.75-.75z"
        clipRule="evenodd"
      />
    </svg>
  );
}

export function PokemonTypeFilter({
  values,
  method,
  availableTypes,
  onChange,
  className,
}: PokemonTypeFilterProps) {
  const containerRef = React.useRef<HTMLDivElement | null>(null);
  const [open, setOpen] = React.useState(false);

  const counts = React.useMemo(() => {
    const map: Record<string, number> = {};
    for (const val of values) {
      const lower = val.trim().toLowerCase();
      map[lower] = (map[lower] || 0) + 1;
    }
    return map;
  }, [values]);

  const hasNoType = Boolean(counts["no-type"] || counts["no-aspect"]);

  const summary = React.useMemo(() => {
    if (values.length === 0) return "Types";
    const parts: string[] = [];
    if (hasNoType) {
      parts.push("No Type");
    }
    for (const type of ORDERED_TYPES) {
      const count = counts[type.toLowerCase()] || 0;
      if (count === 1) parts.push(type);
      else if (count > 1) parts.push(`${type} ×${count}`);
    }
    if (parts.length === 0) return "Types";
    const prefix = method === "exclude" ? "Exclude: " : method === "and" ? "AND: " : "";
    return `Types (${prefix}${parts.join(", ")})`;
  }, [counts, hasNoType, method, values.length]);

  const updateCount = (typeName: string, delta: number) => {
    const lowerName = typeName.toLowerCase();
    const currentCount = counts[lowerName] || 0;
    const nextCount = Math.max(0, currentCount + delta);

    const otherValues = values.filter((v) => v.toLowerCase() !== lowerName);
    const updatedValues = [...otherValues];
    for (let i = 0; i < nextCount; i++) {
      updatedValues.push(typeName);
    }
    onChange(updatedValues, method);
  };

  const toggleNoType = () => {
    if (hasNoType) {
      onChange(
        values.filter((v) => v.toLowerCase() !== "no-type" && v.toLowerCase() !== "no-aspect"),
        method
      );
    } else {
      onChange([...values, "no-aspect"], method);
    }
  };

  const handleMethodChange = (newMethod: AspectMethod) => {
    onChange(values, newMethod);
  };

  const clearAll = () => {
    onChange([], "or");
  };

  React.useEffect(() => {
    if (!open) return;
    const handleClickOutside = (event: MouseEvent) => {
      if (containerRef.current && !containerRef.current.contains(event.target as Node)) {
        setOpen(false);
      }
    };
    const handleKeyDown = (event: KeyboardEvent) => {
      if (event.key === "Escape") {
        setOpen(false);
      }
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
          className={
            open
              ? "-rotate-180 transition-transform ml-2 shrink-0"
              : "transition-transform ml-2 shrink-0"
          }
        />
      </Button>

      {open && (
        <div
          role="dialog"
          aria-label="Filter by Energy Type"
          className={`absolute left-0 z-50 mt-2 rounded-lg border border-white/20 bg-[#242424] shadow-xl p-3 space-y-3 ${
            className?.includes("w-full") ? "w-full" : "w-80"
          }`}
        >
          {/* Method Selector */}
          <div>
            <div className="text-xs font-semibold uppercase tracking-wider text-muted-foreground mb-1.5">
              Filter Method
            </div>
            <div
              className="flex rounded-md border border-white/20 bg-black/30 p-0.5"
              role="group"
              aria-label="Type Filter Method"
            >
              {(["or", "and", "exclude"] as const).map((m) => {
                const isActive = method === m;
                return (
                  <button
                    key={m}
                    type="button"
                    aria-pressed={isActive}
                    onClick={() => handleMethodChange(m)}
                    className={`flex-1 py-1 px-2 text-xs font-semibold rounded transition-colors ${
                      isActive
                        ? m === "exclude"
                          ? "bg-red-600 text-white shadow-sm"
                          : "bg-white/20 text-white shadow-sm"
                        : "text-muted-foreground hover:text-white hover:bg-white/5"
                    }`}
                  >
                    {m.toUpperCase()}
                  </button>
                );
              })}
            </div>
          </div>

          {/* Types List */}
          <div className="space-y-1 max-h-64 overflow-y-auto pr-1">
            <div className="text-xs font-semibold uppercase tracking-wider text-muted-foreground mb-1">
              Energy Types
            </div>
            {ORDERED_TYPES.map((typeName) => {
              const lower = typeName.toLowerCase();
              const count = counts[lower] || 0;
              const isExcluded = method === "exclude" && count > 0;
              const isAvailable =
                !availableTypes ||
                availableTypes.length === 0 ||
                availableTypes.some((t) => t.toLowerCase() === lower);

              return (
                <div
                  key={typeName}
                  className={`flex items-center justify-between py-1 px-1.5 rounded transition-colors ${
                    count > 0
                      ? isExcluded
                        ? "bg-red-950/40 text-red-200"
                        : "bg-white/10 text-white font-medium"
                      : "text-foreground/80 hover:bg-white/5"
                  } ${!isAvailable && count === 0 ? "opacity-50" : ""}`}
                >
                  <div className="flex items-center gap-2">
                    <PokemonEnergyIcon type={typeName} size={18} />
                    <span className="text-sm">{typeName}</span>
                  </div>

                  <div className="flex items-center gap-1.5">
                    <button
                      type="button"
                      aria-label={`Decrease ${typeName}`}
                      disabled={count === 0}
                      onClick={() => updateCount(typeName, -1)}
                      className="flex items-center justify-center h-6 w-6 rounded border border-white/20 bg-white/5 hover:bg-white/15 disabled:opacity-30 disabled:pointer-events-none text-muted-foreground hover:text-white transition-colors"
                    >
                      <MinusIcon />
                    </button>
                    <span
                      className={`text-xs font-bold w-4 text-center ${
                        count > 0
                          ? isExcluded
                            ? "text-red-400"
                            : "text-white"
                          : "text-muted-foreground"
                      }`}
                    >
                      {count}
                    </span>
                    <button
                      type="button"
                      aria-label={`Increase ${typeName}`}
                      onClick={() => updateCount(typeName, 1)}
                      className="flex items-center justify-center h-6 w-6 rounded border border-white/20 bg-white/5 hover:bg-white/15 text-muted-foreground hover:text-white transition-colors"
                    >
                      <PlusIcon />
                    </button>
                  </div>
                </div>
              );
            })}

            {/* No Type Option */}
            <div
              className={`flex items-center justify-between py-1.5 px-1.5 pt-2 mt-1 border-t border-white/10 rounded transition-colors ${
                hasNoType
                  ? "bg-white/10 text-white font-medium"
                  : "text-foreground/80 hover:bg-white/5"
              }`}
            >
              <label
                htmlFor="no-pokemon-type-checkbox"
                className="flex items-center gap-2 text-sm cursor-pointer select-none"
              >
                <input
                  id="no-pokemon-type-checkbox"
                  type="checkbox"
                  checked={hasNoType}
                  onChange={toggleNoType}
                  className="rounded border-white/20 bg-black/40 text-primary focus:ring-1 focus:ring-primary h-3.5 w-3.5"
                />
                <span>Colorless / No Type</span>
              </label>
            </div>
          </div>

          {/* Footer Actions */}
          <div className="flex items-center justify-between pt-2 border-t border-white/10">
            <button
              type="button"
              onClick={clearAll}
              className="text-xs text-muted-foreground hover:text-white transition-colors py-1 px-2 rounded hover:bg-white/5"
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
