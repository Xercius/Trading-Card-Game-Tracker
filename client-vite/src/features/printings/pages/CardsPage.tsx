import * as React from "react";
import { Button } from "@/components/ui/button";
import { usePrintings } from "../api/usePrintings";
import { useCardFilters } from "@/features/cards/filters/useCardFilters";
import FiltersRail from "@/features/cards/filters/FiltersRail";
import PillsBar from "@/features/cards/filters/PillsBar";
import { PrintingCard } from "../components/PrintingCard";

export default function CardsPage() {
  const { filters, clearAll } = useCardFilters();
  const { data, isLoading, isError, error } = usePrintings(filters);
  const printings = data ?? [];
  const [isFiltersOpen, setFiltersOpen] = React.useState(false);

  return (
    <div className="flex h-screen overflow-hidden">
      {/* Desktop Filters Rail - hidden on mobile */}
      <div className="hidden md:block md:w-80 border-r bg-muted/10 overflow-y-auto">
        <FiltersRail onClearAll={clearAll} />
      </div>

      {/* Mobile Filters Drawer */}
      {isFiltersOpen && (
        <div className="fixed inset-0 z-50 bg-background md:hidden overflow-y-auto">
          <FiltersRail onClose={() => setFiltersOpen(false)} onClearAll={clearAll} />
        </div>
      )}

      {/* Main Content */}
      <div className="flex-1 overflow-y-auto">
        <div className="p-4 md:p-6 space-y-4">
          {/* Mobile Filter Toggle Button */}
          <div className="md:hidden">
            <Button onClick={() => setFiltersOpen(true)} variant="outline" className="w-full">
              Filters
            </Button>
          </div>

          {/* Active Filters Pills */}
          <PillsBar />

          {isLoading && <div className="text-sm text-muted-foreground">Loading printings…</div>}
          {isError && <div className="text-sm text-destructive">Error: {error?.message}</div>}

          {!isLoading && !isError && (
            <>
              <div className="text-sm text-muted-foreground">{printings.length} printings</div>
              <ul className="grid gap-3 grid-cols-[repeat(auto-fit,minmax(200px,1fr))]">
                {printings.map(p => (
                  <li key={p.printingId}>
                    <PrintingCard p={p} />
                  </li>
                ))}
              </ul>
            </>
          )}
        </div>
      </div>
    </div>
  );
}
