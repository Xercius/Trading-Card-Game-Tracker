import * as React from "react";
import { createPortal } from "react-dom";
import { Button } from "@/components/ui/button";
import type { CardFilters } from "@/features/cards/filters/useCardFilters";
import type { PrintingFacets } from "../api/usePrintingFacets";
import { MtgFiltersSection } from "./filters/MtgFiltersSection";
import { PokemonFiltersSection } from "./filters/PokemonFiltersSection";
import { SwccgFiltersSection } from "./filters/SwccgFiltersSection";
import { SwuFiltersSection } from "./filters/SwuFiltersSection";

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
 * Slide-out filter drawer housing game-scoped facet controls and clear filters action.
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
  const isSwccg = game === "Star Wars CCG";
  const isPokemon = game === "Pokemon TCG";
  const isMtg = game === "Magic The Gathering";

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
      <div
        className="fixed inset-0 bg-black/60 backdrop-blur-xs transition-opacity"
        onClick={() => onOpenChange(false)}
        aria-hidden="true"
      />
      <div
        ref={drawerRef}
        tabIndex={-1}
        className="relative z-10 flex h-full w-full max-w-sm flex-col bg-[#141416] text-white shadow-2xl outline-hidden animate-in slide-in-from-right duration-200"
      >
        <div className="flex items-center justify-between border-b border-white/10 px-5 py-4">
          <div className="flex items-center gap-2">
            <h2 id="card-filters-drawer-title" className="text-lg font-bold tracking-tight">
              Filters
            </h2>
            <span className="text-xs text-muted-foreground uppercase tracking-wider">
              {game || "Star Wars Unlimited"}
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
            <SwccgFiltersSection
              filters={filters}
              setFilters={setFilters}
              availableFacets={availableFacets}
            />
          ) : isPokemon ? (
            <PokemonFiltersSection
              filters={filters}
              setFilters={setFilters}
              availableFacets={availableFacets}
            />
          ) : isMtg ? (
            <MtgFiltersSection
              filters={filters}
              setFilters={setFilters}
              availableFacets={availableFacets}
            />
          ) : (
            <SwuFiltersSection
              filters={filters}
              setFilters={setFilters}
              availableFacets={availableFacets}
            />
          )}
        </div>

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
