import * as React from "react";
import { Button } from "@/components/ui/button";
import type { AspectMethod } from "@/features/cards/filters/useCardFilters";

export type AspectFilterProps = {
  values: string[];
  method: AspectMethod;
  availableAspects?: string[];
  onChange: (values: string[], method: AspectMethod) => void;
  className?: string;
};

type AspectDef = {
  id: string;
  name: string;
  colorDot: string;
};

const SWU_ASPECTS: AspectDef[] = [
  { id: "vigilance", name: "Vigilance", colorDot: "bg-blue-500" },
  { id: "command", name: "Command", colorDot: "bg-green-600" },
  { id: "aggression", name: "Aggression", colorDot: "bg-red-500" },
  { id: "cunning", name: "Cunning", colorDot: "bg-yellow-500" },
  { id: "heroism", name: "Heroism", colorDot: "bg-slate-100 text-black border border-slate-300" },
  { id: "villainy", name: "Villainy", colorDot: "bg-slate-900 border border-slate-600" },
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

/**
 * Filter Star Wars: Unlimited cards by Aspect, supporting multiplicity (e.g. Double Aspect),
 * No Aspect (neutral) cards, and filter methods (OR, AND, EXCLUDE).
 */
export function AspectFilter({ values, method, onChange, className }: AspectFilterProps) {
  const containerRef = React.useRef<HTMLDivElement | null>(null);
  const [open, setOpen] = React.useState(false);

  // Group values into counts
  const counts = React.useMemo(() => {
    const map: Record<string, number> = {};
    for (const val of values) {
      const lower = val.trim().toLowerCase();
      map[lower] = (map[lower] || 0) + 1;
    }
    return map;
  }, [values]);

  const hasNoAspect = Boolean(counts["no-aspect"]);

  // Calculate distinct aspects and total count for button summary
  const summary = React.useMemo(() => {
    if (values.length === 0) return "Aspects";
    const parts: string[] = [];
    if (hasNoAspect) {
      parts.push("No Aspect");
    }
    for (const aspect of SWU_ASPECTS) {
      const count = counts[aspect.id] || 0;
      if (count === 1) parts.push(aspect.name);
      else if (count > 1) parts.push(`${aspect.name} ×${count}`);
    }
    if (parts.length === 0) return "Aspects";
    const prefix = method === "exclude" ? "Exclude: " : method === "and" ? "AND: " : "";
    return `Aspects (${prefix}${parts.join(", ")})`;
  }, [counts, hasNoAspect, method, values.length]);

  const updateCount = (aspectName: string, delta: number) => {
    const lowerName = aspectName.toLowerCase();
    const currentCount = counts[lowerName] || 0;
    const nextCount = Math.max(0, currentCount + delta);

    // Reconstruct values list
    const otherValues = values.filter((v) => v.toLowerCase() !== lowerName);
    const updatedValues = [...otherValues];
    for (let i = 0; i < nextCount; i++) {
      updatedValues.push(aspectName);
    }
    onChange(updatedValues, method);
  };

  const toggleNoAspect = () => {
    if (hasNoAspect) {
      onChange(
        values.filter((v) => v.toLowerCase() !== "no-aspect"),
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
          aria-label="Filter by Aspect"
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
              aria-label="Aspect Filter Method"
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

          {/* Aspect Steppers */}
          <div className="space-y-1.5 border-t border-white/10 pt-2">
            <div className="text-xs font-semibold uppercase tracking-wider text-muted-foreground mb-1">
              Aspects
            </div>
            {SWU_ASPECTS.map((aspect) => {
              const count = counts[aspect.id] || 0;
              const isSelected = count > 0;
              return (
                <div
                  key={aspect.id}
                  className={`flex items-center justify-between px-2.5 py-1.5 rounded-md transition-colors ${
                    isSelected ? "bg-white/10" : "hover:bg-white/5"
                  }`}
                >
                  <div className="flex items-center gap-2">
                    <span className={`inline-block h-3.5 w-3.5 rounded-full ${aspect.colorDot}`} />
                    <span
                      className={`text-sm ${isSelected ? "font-semibold text-white" : "text-foreground"}`}
                    >
                      {aspect.name}
                    </span>
                  </div>

                  <div className="flex items-center gap-1.5">
                    <button
                      type="button"
                      aria-label={`Decrease ${aspect.name}`}
                      disabled={count === 0}
                      onClick={() => updateCount(aspect.name, -1)}
                      className="inline-flex h-6 w-6 items-center justify-center rounded border border-white/20 text-white hover:bg-white/10 disabled:opacity-30 disabled:pointer-events-none"
                    >
                      <MinusIcon />
                    </button>
                    <span
                      aria-label={`${aspect.name} count`}
                      className={`min-w-6 text-center text-xs font-bold ${
                        isSelected ? "text-white" : "text-muted-foreground"
                      }`}
                    >
                      {count}
                    </span>
                    <button
                      type="button"
                      aria-label={`Increase ${aspect.name}`}
                      onClick={() => updateCount(aspect.name, 1)}
                      className="inline-flex h-6 w-6 items-center justify-center rounded border border-white/20 text-white hover:bg-white/10"
                    >
                      <PlusIcon />
                    </button>
                  </div>
                </div>
              );
            })}
          </div>

          {/* No Aspect Option */}
          <div className="border-t border-white/10 pt-2">
            <label className="flex items-center justify-between px-2.5 py-1.5 rounded-md hover:bg-white/5 cursor-pointer">
              <span
                className={`text-sm ${hasNoAspect ? "font-semibold text-white" : "text-foreground"}`}
              >
                No Aspect (Neutral)
              </span>
              <input
                type="checkbox"
                checked={hasNoAspect}
                onChange={toggleNoAspect}
                aria-label="No Aspect (Neutral)"
                className="h-4 w-4 rounded border-white/30 bg-black/20 text-primary focus:ring-primary focus:ring-offset-0 cursor-pointer"
              />
            </label>
          </div>

          {/* Footer actions */}
          <div className="border-t border-white/10 pt-2 flex items-center justify-between">
            <Button
              type="button"
              variant="ghost"
              size="sm"
              onClick={clearAll}
              disabled={values.length === 0 && method === "or"}
              className="text-xs h-7 px-2 text-muted-foreground hover:text-white"
            >
              Reset
            </Button>
            <Button
              type="button"
              size="sm"
              onClick={() => setOpen(false)}
              className="text-xs h-7 px-3 bg-white/20 hover:bg-white/30 text-white"
            >
              Done
            </Button>
          </div>
        </div>
      )}
    </div>
  );
}
