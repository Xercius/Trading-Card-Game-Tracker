import * as React from "react";
import { createPortal } from "react-dom";
import { Button } from "@/components/ui/button";
import { MultiSelect } from "./MultiSelect";
import { AspectFilter } from "./AspectFilter";
import { Input } from "@/components/ui/input";
import { LightsaberBlade } from "@/features/cards/components/SwccgForceIcons";
import type { CardFilters } from "@/features/cards/filters/useCardFilters";
import type { PrintingFacets } from "../api/usePrintingFacets";

export type CardFiltersDrawerProps = {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  filters: CardFilters;
  setFilters: React.Dispatch<React.SetStateAction<CardFilters>>;
  availableFacets: PrintingFacets;
  isFacetsLoading: boolean;
  isFacetsError: boolean;
  onRetryFacets: () => void;
  onClearFilters: () => void;
  hasActiveFilters: boolean;
  game?: string;
};

type NumericFilterKey = "powers" | "abilities" | "lightSideIcons" | "darkSideIcons";
type NumericFilterDrafts = Record<NumericFilterKey, string>;

function getNumericFilterDrafts(filters: CardFilters): NumericFilterDrafts {
  return {
    powers: filters.powers?.join(", ") ?? "",
    abilities: filters.abilities?.join(", ") ?? "",
    lightSideIcons: filters.lightSideIcons?.join(", ") ?? "",
    darkSideIcons: filters.darkSideIcons?.join(", ") ?? "",
  };
}

const FOCUSABLE = [
  "a[href]",
  "button:not([disabled])",
  "textarea:not([disabled])",
  "input:not([disabled])",
  "select:not([disabled])",
  "[tabindex]:not([tabindex='-1'])",
].join(",");

function CloseIcon({ className = "" }: { className?: string }) {
  return (
    <svg
      aria-hidden="true"
      className={`h-5 w-5 ${className}`}
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      strokeWidth="2"
      strokeLinecap="round"
      strokeLinejoin="round"
    >
      <line x1="18" y1="6" x2="6" y2="18" />
      <line x1="6" y1="6" x2="18" y2="18" />
    </svg>
  );
}

/**
 * Slide-out filter drawer inspired by Star Wars: Unlimited card search page.
 * Houses all facet controls and a clear filters action.
 */
export function CardFiltersDrawer({
  open,
  onOpenChange,
  filters,
  setFilters,
  availableFacets,
  isFacetsLoading,
  isFacetsError,
  onRetryFacets,
  onClearFilters,
  hasActiveFilters,
  game,
}: CardFiltersDrawerProps) {
  const drawerRef = React.useRef<HTMLDivElement | null>(null);
  const lastFocusedRef = React.useRef<HTMLElement | null>(null);
  const [numericFilterDrafts, setNumericFilterDrafts] = React.useState(() =>
    getNumericFilterDrafts(filters)
  );
  const isSwccg = game === "Star Wars CCG";
  const activeSide = filters.side
    ? filters.side.toLowerCase() === "light"
      ? "Light"
      : filters.side.toLowerCase() === "dark"
        ? "Dark"
        : null
    : null;

  React.useEffect(() => {
    setNumericFilterDrafts({
      powers: filters.powers?.join(", ") ?? "",
      abilities: filters.abilities?.join(", ") ?? "",
      lightSideIcons: filters.lightSideIcons?.join(", ") ?? "",
      darkSideIcons: filters.darkSideIcons?.join(", ") ?? "",
    });
  }, [filters.powers, filters.abilities, filters.lightSideIcons, filters.darkSideIcons]);

  const commitNumericFilter = (key: NumericFilterKey) => {
    const values = numericFilterDrafts[key]
      .split(",")
      .map((value) => value.trim())
      .filter(Boolean);
    setFilters((prev) => ({ ...prev, [key]: values, page: 1 }));
  };

  React.useEffect(() => {
    if (!open) return;
    lastFocusedRef.current =
      document.activeElement instanceof HTMLElement ? document.activeElement : null;
    const frame = requestAnimationFrame(() => {
      const drawer = drawerRef.current;
      if (!drawer) return;
      const focusables = Array.from(drawer.querySelectorAll<HTMLElement>(FOCUSABLE));
      (focusables[0] ?? drawer).focus();
    });
    return () => cancelAnimationFrame(frame);
  }, [open]);

  React.useEffect(() => {
    if (open) return;
    const previous = lastFocusedRef.current;
    if (previous?.isConnected) previous.focus();
  }, [open]);

  React.useEffect(() => {
    if (!open) return;

    const originalOverflow = document.body.style.overflow;
    document.body.style.overflow = "hidden";

    const handleKeyDown = (event: KeyboardEvent) => {
      if (event.key === "Escape") {
        event.preventDefault();
        onOpenChange(false);
        return;
      }
      if (event.key !== "Tab") return;
      const drawer = drawerRef.current;
      if (!drawer) return;
      const focusables = Array.from(drawer.querySelectorAll<HTMLElement>(FOCUSABLE));
      if (focusables.length === 0) return;
      const first = focusables[0];
      const last = focusables[focusables.length - 1];
      if (!drawer.contains(document.activeElement)) {
        event.preventDefault();
        (event.shiftKey ? last : first).focus();
      } else if (event.shiftKey && document.activeElement === first) {
        event.preventDefault();
        last.focus();
      } else if (!event.shiftKey && document.activeElement === last) {
        event.preventDefault();
        first.focus();
      }
    };

    window.addEventListener("keydown", handleKeyDown);
    return () => {
      document.body.style.overflow = originalOverflow;
      window.removeEventListener("keydown", handleKeyDown);
    };
  }, [open, onOpenChange]);

  if (!open) return null;

  return createPortal(
    <div
      role="dialog"
      aria-modal="true"
      aria-labelledby="card-filters-drawer-title"
      className="fixed inset-0 z-50 flex justify-end"
    >
      {/* Backdrop */}
      <div
        className="fixed inset-0 bg-black/60 backdrop-blur-xs transition-opacity"
        onClick={() => onOpenChange(false)}
        aria-hidden="true"
      />

      {/* Drawer Panel */}
      <div
        ref={drawerRef}
        tabIndex={-1}
        className="relative z-10 flex h-full w-full max-w-sm flex-col bg-[#141416] text-white shadow-2xl outline-hidden animate-in slide-in-from-right duration-200"
      >
        {/* Header */}
        <div className="flex items-center justify-between border-b border-white/10 px-5 py-4">
          <div className="flex items-center gap-2">
            <h2 id="card-filters-drawer-title" className="text-lg font-bold tracking-tight">
              Filters
            </h2>
            <span className="text-xs text-muted-foreground uppercase tracking-wider">
              {isSwccg ? "Star Wars CCG" : "Star Wars Unlimited"}
            </span>
          </div>
          <Button
            type="button"
            variant="ghost"
            size="sm"
            aria-label="Close filters"
            onClick={() => onOpenChange(false)}
            className="h-8 w-8 rounded-full p-0 text-white/70 hover:bg-white/10 hover:text-white"
          >
            <CloseIcon />
          </Button>
        </div>

        {/* Content */}
        <div className="flex-1 space-y-2.5 overflow-y-auto p-5">
          {isFacetsLoading && <div className="text-sm text-muted-foreground">Loading filters…</div>}
          {isFacetsError && (
            <div className="flex items-center gap-2 text-sm text-destructive">
              <span>Unable to load filters.</span>
              <Button variant="outline" size="sm" onClick={onRetryFacets}>
                Retry filters
              </Button>
            </div>
          )}

          {isSwccg ? (
            <>
              {/* Dark / Light Toggle */}
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

              {/* Card Type */}
              <MultiSelect
                label="Type"
                className="w-full justify-between"
                values={filters.types ?? []}
                options={availableFacets.types ?? []}
                placeholder="Search types…"
                onChange={(values) => setFilters((prev) => ({ ...prev, types: values, page: 1 }))}
              />

              {/* Sub-Type */}
              <MultiSelect
                label="Sub-Type"
                className="w-full justify-between"
                values={filters.subTypes ?? []}
                options={availableFacets.subTypes ?? []}
                placeholder="Search sub-types…"
                onChange={(values) =>
                  setFilters((prev) => ({ ...prev, subTypes: values, page: 1 }))
                }
              />

              {/* Persona */}
              <MultiSelect
                label="Persona"
                className="w-full justify-between"
                values={filters.personas ?? []}
                options={availableFacets.personas ?? []}
                placeholder="Search personas…"
                onChange={(values) =>
                  setFilters((prev) => ({ ...prev, personas: values, page: 1 }))
                }
              />

              {/* Set */}
              <MultiSelect
                label="Set"
                className="w-full justify-between"
                values={filters.sets ?? []}
                options={availableFacets.sets ?? []}
                placeholder="Search sets…"
                onChange={(vals) => setFilters((prev) => ({ ...prev, sets: vals, page: 1 }))}
              />

              {/* Rarity */}
              <MultiSelect
                label="Rarity"
                className="w-full justify-between"
                values={filters.rarities ?? []}
                options={availableFacets.rarities ?? []}
                placeholder="Search rarities…"
                onChange={(vals) => setFilters((prev) => ({ ...prev, rarities: vals, page: 1 }))}
              />

              {/* Traits */}
              <MultiSelect
                label="Traits"
                className="w-full justify-between"
                values={filters.traits ?? []}
                options={availableFacets.traits ?? []}
                placeholder="Search traits…"
                method={filters.traitMethod ?? "or"}
                onMethodChange={(method) =>
                  setFilters((prev) => ({
                    ...prev,
                    traitMethod: method,
                    page: 1,
                  }))
                }
                onChange={(values) =>
                  setFilters((prev) => ({
                    ...prev,
                    traits: values,
                    ...(values.length === 0 ? { traitMethod: "or" as const } : {}),
                    page: 1,
                  }))
                }
              />

              {/* Destiny */}
              <MultiSelect
                label="Destiny"
                className="w-full justify-between"
                values={filters.destinies ?? []}
                options={availableFacets.destinies ?? []}
                placeholder="Search destinies…"
                onChange={(vals) => setFilters((prev) => ({ ...prev, destinies: vals, page: 1 }))}
              />

              {/* Deploy */}
              <MultiSelect
                label="Deploy"
                className="w-full justify-between"
                values={filters.deploys ?? []}
                options={availableFacets.deploys ?? []}
                placeholder="Search deploys…"
                onChange={(vals) => setFilters((prev) => ({ ...prev, deploys: vals, page: 1 }))}
              />

              {/* Forfeit */}
              <MultiSelect
                label="Forfeit"
                className="w-full justify-between"
                values={filters.forfeits ?? []}
                options={availableFacets.forfeits ?? []}
                placeholder="Search forfeits…"
                onChange={(vals) => setFilters((prev) => ({ ...prev, forfeits: vals, page: 1 }))}
              />

              {/* Power & Ability */}
              <div className="grid grid-cols-2 gap-2">
                <div className="flex items-center gap-2">
                  <label
                    htmlFor="filter-power"
                    className="text-xs font-semibold text-muted-foreground w-12 shrink-0"
                  >
                    Power
                  </label>
                  <Input
                    id="filter-power"
                    placeholder="e.g. >2"
                    className="h-8 text-xs bg-card/40 border-white/20"
                    value={numericFilterDrafts.powers}
                    onChange={(e) =>
                      setNumericFilterDrafts((prev) => ({ ...prev, powers: e.target.value }))
                    }
                    onBlur={() => commitNumericFilter("powers")}
                  />
                </div>
                <div className="flex items-center gap-2">
                  <label
                    htmlFor="filter-ability"
                    className="text-xs font-semibold text-muted-foreground w-12 shrink-0"
                  >
                    Ability
                  </label>
                  <Input
                    id="filter-ability"
                    placeholder="e.g. >2"
                    className="h-8 text-xs bg-card/40 border-white/20"
                    value={numericFilterDrafts.abilities}
                    onChange={(e) =>
                      setNumericFilterDrafts((prev) => ({ ...prev, abilities: e.target.value }))
                    }
                    onBlur={() => commitNumericFilter("abilities")}
                  />
                </div>
              </div>

              {/* Light Side & Dark Side Icons */}
              <div className="grid grid-cols-2 gap-2">
                <div className="flex items-center gap-2" title="Light Side Icons">
                  <div className="flex items-center justify-center w-5 shrink-0" aria-hidden="true">
                    <LightsaberBlade side="light" size="sm" />
                  </div>
                  <Input
                    id="filter-light-side-icons"
                    aria-label="Light Side Icons"
                    placeholder="e.g. >1"
                    className="h-8 text-xs bg-card/40 border-white/20"
                    value={numericFilterDrafts.lightSideIcons}
                    onChange={(e) =>
                      setNumericFilterDrafts((prev) => ({
                        ...prev,
                        lightSideIcons: e.target.value,
                      }))
                    }
                    onBlur={() => commitNumericFilter("lightSideIcons")}
                  />
                </div>
                <div className="flex items-center gap-2" title="Dark Side Icons">
                  <div className="flex items-center justify-center w-5 shrink-0" aria-hidden="true">
                    <LightsaberBlade side="dark" size="sm" />
                  </div>
                  <Input
                    id="filter-dark-side-icons"
                    aria-label="Dark Side Icons"
                    placeholder="e.g. >1"
                    className="h-8 text-xs bg-card/40 border-white/20"
                    value={numericFilterDrafts.darkSideIcons}
                    onChange={(e) =>
                      setNumericFilterDrafts((prev) => ({ ...prev, darkSideIcons: e.target.value }))
                    }
                    onBlur={() => commitNumericFilter("darkSideIcons")}
                  />
                </div>
              </div>
            </>
          ) : (
            <>
              {/* SWU Filters */}
              <AspectFilter
                className="w-full justify-between"
                values={filters.aspects ?? []}
                method={filters.aspectMethod ?? "or"}
                availableAspects={availableFacets.aspects ?? []}
                onChange={(values, method) =>
                  setFilters((prev) => ({
                    ...prev,
                    aspects: values,
                    aspectMethod: method,
                    page: 1,
                  }))
                }
              />

              <MultiSelect
                label="Type"
                className="w-full justify-between"
                values={filters.types ?? []}
                options={availableFacets.types ?? []}
                placeholder="Search types…"
                onChange={(values) => setFilters((prev) => ({ ...prev, types: values, page: 1 }))}
              />

              <MultiSelect
                label="Set"
                className="w-full justify-between"
                values={filters.sets ?? []}
                options={availableFacets.sets}
                placeholder="Search sets…"
                onChange={(vals) => setFilters((prev) => ({ ...prev, sets: vals, page: 1 }))}
              />

              <MultiSelect
                label="Rarity"
                className="w-full justify-between"
                values={filters.rarities ?? []}
                options={availableFacets.rarities}
                onChange={(vals) => setFilters((prev) => ({ ...prev, rarities: vals, page: 1 }))}
              />

              <MultiSelect
                label="Arena"
                className="w-full justify-between"
                values={filters.arenas ?? []}
                options={availableFacets.arenas ?? []}
                placeholder="Search arenas…"
                onChange={(values) => setFilters((prev) => ({ ...prev, arenas: values, page: 1 }))}
              />

              <MultiSelect
                label="Traits"
                className="w-full justify-between"
                values={filters.traits ?? []}
                options={availableFacets.traits ?? []}
                placeholder="Search traits…"
                method={filters.traitMethod ?? "or"}
                onMethodChange={(method) =>
                  setFilters((prev) => ({
                    ...prev,
                    traitMethod: method,
                    page: 1,
                  }))
                }
                onChange={(values) =>
                  setFilters((prev) => ({
                    ...prev,
                    traits: values,
                    ...(values.length === 0 ? { traitMethod: "or" as const } : {}),
                    page: 1,
                  }))
                }
              />

              <MultiSelect
                label="Keywords"
                className="w-full justify-between"
                values={filters.keywords ?? []}
                options={availableFacets.keywords ?? []}
                placeholder="Search keywords…"
                method={filters.keywordMethod ?? "or"}
                onMethodChange={(method) =>
                  setFilters((prev) => ({
                    ...prev,
                    keywordMethod: method,
                    page: 1,
                  }))
                }
                onChange={(values) =>
                  setFilters((prev) => ({
                    ...prev,
                    keywords: values,
                    ...(values.length === 0 ? { keywordMethod: "or" as const } : {}),
                    page: 1,
                  }))
                }
              />
            </>
          )}
        </div>

        {/* Footer */}
        <div className="flex items-center justify-between border-t border-white/10 bg-[#141416] p-4">
          <Button
            type="button"
            variant="outline"
            onClick={onClearFilters}
            disabled={!hasActiveFilters}
            className="border-white/20 bg-transparent text-white/90 hover:bg-white/10 hover:text-white"
          >
            Clear Filters
          </Button>
          <Button
            type="button"
            onClick={() => onOpenChange(false)}
            className="bg-blue-600 px-6 font-semibold text-white hover:bg-blue-700"
          >
            Close
          </Button>
        </div>
      </div>
    </div>,
    document.body
  );
}
