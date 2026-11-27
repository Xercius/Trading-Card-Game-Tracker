import * as React from "react";
import { Button } from "@/components/ui/button";
import { MtgColorCheckboxes } from "./MtgColorFilter";
import { MTG_COLORS } from "./mtgColors";

export type MtgColorIdentityFilterProps = {
  values: string[];
  availableColors?: string[];
  onChange: (values: string[]) => void;
  className?: string;
};

/**
 * Filter Magic: The Gathering cards by color identity (White, Blue, Black, Red, Green, Colorless),
 * matching the Colors filter UI without an operator selector (backend excludes unselected colors).
 */
export function MtgColorIdentityFilter({
  values,
  availableColors,
  onChange,
  className,
}: MtgColorIdentityFilterProps) {
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
    if (selectedNames.length === 0) return "Color Identity";
    return `Color Identity (${selectedNames.join(", ")})`;
  }, [selectedSet]);

  const toggleColor = (colorName: string) => {
    const key = colorName.toLowerCase();
    const next = selectedSet.has(key)
      ? values.filter((v) => v.trim().toLowerCase() !== key)
      : [...values, colorName];
    onChange(next);
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
        <span
          aria-hidden="true"
          className={`ml-2 text-xs transition-transform ${open ? "rotate-180" : ""}`}
        >
          ▾
        </span>
      </Button>

      {open && (
        <div
          role="dialog"
          aria-label="Filter by Color Identity"
          className={`absolute left-0 z-50 mt-2 space-y-3 rounded-lg border border-white/20 bg-[#242424] p-3 shadow-xl ${
            className?.includes("w-full") ? "w-full" : "w-80"
          }`}
        >
          <MtgColorCheckboxes
            idPrefix="mtg-color-identity"
            values={values}
            availableColors={availableColors}
            onToggle={toggleColor}
          />

          {values.length > 0 && (
            <div className="border-t border-white/10 pt-2">
              <button
                type="button"
                onClick={() => onChange([])}
                className="w-full rounded py-1 text-center text-xs text-muted-foreground hover:bg-white/5 hover:text-foreground"
              >
                Clear color identity
              </button>
            </div>
          )}
        </div>
      )}
    </div>
  );
}
