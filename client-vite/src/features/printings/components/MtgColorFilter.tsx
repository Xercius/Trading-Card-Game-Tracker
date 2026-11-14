import * as React from "react";
import { Button } from "@/components/ui/button";
import type { AspectMethod } from "@/features/cards/filters/useCardFilters";
import { MtgSymbolIcon } from "./MtgSymbolIcon";

export type MtgColorFilterProps = {
  values: string[];
  method: AspectMethod;
  availableColors?: string[];
  onChange: (values: string[], method: AspectMethod) => void;
  className?: string;
};

const MTG_COLORS = [
  { name: "White", symbol: "{W}" },
  { name: "Blue", symbol: "{U}" },
  { name: "Black", symbol: "{B}" },
  { name: "Red", symbol: "{R}" },
  { name: "Green", symbol: "{G}" },
  { name: "Colorless", symbol: "{C}" },
] as const;

const METHODS: { value: AspectMethod; label: string }[] = [
  { value: "or", label: "OR" },
  { value: "and", label: "AND" },
  { value: "exclude", label: "Exclude" },
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

/**
 * Filter Magic: The Gathering cards by color (White, Blue, Black, Red, Green, Colorless),
 * supporting OR, AND, and Exclude filter methods while backing onto the shared `aspects` parameter.
 */
export function MtgColorFilter({
  values,
  method,
  availableColors,
  onChange,
  className,
}: MtgColorFilterProps) {
  const containerRef = React.useRef<HTMLDivElement | null>(null);
  const [open, setOpen] = React.useState(false);

  const selectedSet = React.useMemo(
    () => new Set(values.map((v) => v.trim().toLowerCase())),
    [values]
  );

  const summary = React.useMemo(() => {
    const selectedNames = MTG_COLORS.filter((c) => selectedSet.has(c.name.toLowerCase())).map(
      (c) => c.name
    );
    if (selectedNames.length === 0) return "Colors";
    const prefix = method === "exclude" ? "Exclude: " : method === "and" ? "AND: " : "";
    return `Colors (${prefix}${selectedNames.join(", ")})`;
  }, [method, selectedSet]);

  const toggleColor = (colorName: string) => {
    const key = colorName.toLowerCase();
    const next = selectedSet.has(key)
      ? values.filter((v) => v.trim().toLowerCase() !== key)
      : [...values, colorName];
    onChange(next, next.length === 0 ? "or" : method);
  };

  React.useEffect(() => {
    if (!open) return;
    const onOutside = (e: MouseEvent) => {
      if (containerRef.current && !containerRef.current.contains(e.target as Node)) {
        setOpen(false);
      }
    };
    const onEscape = (e: KeyboardEvent) => {
      if (e.key === "Escape") setOpen(false);
    };
    document.addEventListener("mousedown", onOutside);
    document.addEventListener("keydown", onEscape);
    return () => {
      document.removeEventListener("mousedown", onOutside);
      document.removeEventListener("keydown", onEscape);
    };
  }, [open]);

  return (
    <div ref={containerRef} className="relative">
      <Button
        type="button"
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
          aria-label="Filter by Color"
          className={`absolute left-0 z-50 mt-2 space-y-3 rounded-lg border border-white/20 bg-[#242424] p-3 shadow-xl ${
            className?.includes("w-full") ? "w-full" : "w-80"
          }`}
        >
          <div>
            <div className="mb-1.5 text-xs font-semibold uppercase tracking-wider text-muted-foreground">
              Filter Method
            </div>
            <div
              className="flex rounded-md border border-white/20 bg-black/30 p-0.5"
              role="group"
              aria-label="Color filter method"
            >
              {METHODS.map((m) => (
                <button
                  key={m.value}
                  type="button"
                  aria-pressed={method === m.value}
                  onClick={() => onChange(values, m.value)}
                  className={`flex-1 rounded px-2 py-1 text-xs font-medium transition-colors ${
                    method === m.value
                      ? "bg-primary text-primary-foreground shadow-xs"
                      : "text-muted-foreground hover:text-foreground"
                  }`}
                >
                  {m.label}
                </button>
              ))}
            </div>
          </div>

          <div className="space-y-1">
            <div className="mb-1 text-xs font-semibold uppercase tracking-wider text-muted-foreground">
              Colors
            </div>
            {MTG_COLORS.map((color) => {
              const isSelected = selectedSet.has(color.name.toLowerCase());
              const isAvailable =
                !availableColors ||
                availableColors.length === 0 ||
                availableColors.some((c) => c.trim().toLowerCase() === color.name.toLowerCase());

              return (
                <button
                  key={color.name}
                  type="button"
                  aria-label={color.name}
                  aria-pressed={isSelected}
                  onClick={() => toggleColor(color.name)}
                  className={`flex w-full items-center gap-2.5 rounded px-1.5 py-1 text-left text-sm transition-colors ${
                    isSelected
                      ? "bg-white/10 font-medium text-white"
                      : "text-foreground/80 hover:bg-white/5"
                  } ${!isAvailable && !isSelected ? "opacity-50" : ""}`}
                >
                  <MtgSymbolIcon symbol={color.symbol} size={16} />
                  <span>{color.name}</span>
                </button>
              );
            })}
          </div>

          {values.length > 0 && (
            <div className="border-t border-white/10 pt-2">
              <button
                type="button"
                onClick={() => onChange([], "or")}
                className="w-full rounded py-1 text-center text-xs text-muted-foreground hover:bg-white/5 hover:text-foreground"
              >
                Clear colors
              </button>
            </div>
          )}
        </div>
      )}
    </div>
  );
}
