import * as React from "react";
import { createPortal } from "react-dom";
import { Button } from "@/components/ui/button";
import { MultiSelect } from "./MultiSelect";
import { AspectFilter } from "./AspectFilter";
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
};

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
}: CardFiltersDrawerProps) {
  const drawerRef = React.useRef<HTMLDivElement | null>(null);
  const lastFocusedRef = React.useRef<HTMLElement | null>(null);

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
      if (focusables.length === 0) {
        event.preventDefault();
        drawer.focus();
        return;
      }
      const first = focusables[0];
      const last = focusables[focusables.length - 1];
      const active = document.activeElement as HTMLElement | null;
      if (event.shiftKey && (active === first || !drawer.contains(active))) {
        event.preventDefault();
        last.focus();
      } else if (!event.shiftKey && (active === last || !drawer.contains(active))) {
        event.preventDefault();
        first.focus();
      }
    };

    document.addEventListener("keydown", handleKeyDown);
    return () => {
      document.body.style.overflow = originalOverflow;
      document.removeEventListener("keydown", handleKeyDown);
    };
  }, [open, onOpenChange]);

  if (!open) return null;

  return createPortal(
    <div className="fixed inset-0 z-50 flex justify-end" role="presentation">
      {/* Backdrop */}
      <div
        className="fixed inset-0 bg-black/60 transition-opacity"
        aria-hidden="true"
        onClick={() => onOpenChange(false)}
      />

      {/* Drawer Surface */}
      <div
        ref={drawerRef}
        role="dialog"
        aria-modal="true"
        aria-labelledby="card-filters-drawer-title"
        tabIndex={-1}
        className="relative z-50 flex h-full w-full max-w-md flex-col border-l border-white/10 bg-[#18181b] text-foreground shadow-2xl focus:outline-none"
      >
        {/* Header */}
        <div className="flex items-center justify-between border-b border-white/10 px-5 py-4">
          <h2
            id="card-filters-drawer-title"
            className="text-xl font-bold tracking-tight text-white"
          >
            Filters
          </h2>
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
            onChange={(values) => setFilters((prev) => ({ ...prev, traits: values, page: 1 }))}
          />
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
