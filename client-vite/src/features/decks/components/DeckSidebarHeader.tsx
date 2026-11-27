import { Button } from "@/components/ui/button";
import LineSparkline from "@/components/charts/LineSparkline";
import type { ValuePoint } from "@/types/value";

type Props = {
  deckName: string;
  total: number;
  sideboardTotal: number;
  hasSideboard: boolean;
  includeProxies: boolean;
  onIncludeProxiesChange: (next: boolean) => void;
  totalMissingCopies: number;
  missingOnly: boolean;
  onToggleMissingOnly: () => void;
  onAddMissingToWishlist?: () => void;
  isAddingMissing?: boolean;
  onOpenImport?: () => void;
  onOpenExport?: () => void;
  validationIssues?: string[];
  valueHistory: {
    points: ValuePoint[];
    isLoading: boolean;
    isError: boolean;
    latestValue: number | null;
  };
  formatCurrency: (value: number | null) => string;
};

function ValueHistoryBox({
  valueHistory,
  formatCurrency,
}: Pick<Props, "valueHistory" | "formatCurrency">) {
  const hasValueHistory = valueHistory.points.length > 0;
  return (
    <div className="mt-4 rounded border border-border/60 bg-background p-3">
      <div className="flex items-center justify-between text-xs text-muted-foreground">
        <span>Deck value (last 90 days)</span>
        <span>Proxies excluded.</span>
      </div>
      {valueHistory.isLoading && (
        <p className="mt-2 text-xs text-muted-foreground">Loading value history…</p>
      )}
      {valueHistory.isError && (
        <p className="mt-2 text-xs text-destructive">Failed to load value history.</p>
      )}
      {!valueHistory.isLoading && !valueHistory.isError && !hasValueHistory && (
        <p className="mt-2 text-xs text-muted-foreground">No value data.</p>
      )}
      {hasValueHistory && (
        <div className="mt-2 space-y-2">
          <LineSparkline
            points={valueHistory.points}
            ariaLabel="Deck value over time"
            height={72}
            className="h-20"
            stroke="hsl(var(--primary))"
          />
          <p className="text-xs text-muted-foreground">
            Latest: {formatCurrency(valueHistory.latestValue)}
          </p>
        </div>
      )}
    </div>
  );
}

/**
 * Header block for StickyDeckSidebar showing counts, quick filters, import/export actions, and validation status.
 */
export function DeckSidebarHeader({
  deckName,
  total,
  sideboardTotal,
  hasSideboard,
  includeProxies,
  onIncludeProxiesChange,
  totalMissingCopies,
  missingOnly,
  onToggleMissingOnly,
  onAddMissingToWishlist,
  isAddingMissing = false,
  onOpenImport,
  onOpenExport,
  validationIssues,
  valueHistory,
  formatCurrency,
}: Props) {
  return (
    <div className="border-b p-4">
      <div className="flex items-start justify-between gap-2">
        <div>
          <h2 className="text-lg font-semibold">{deckName || "Deck"}</h2>
          <div className="mt-1 text-sm text-muted-foreground">
            Total cards: {total}
            {hasSideboard && sideboardTotal > 0 ? ` · SB: ${sideboardTotal}` : ""}
          </div>
        </div>
        {(onOpenImport || onOpenExport) && (
          <div className="flex items-center gap-1.5">
            {onOpenImport && (
              <Button size="sm" variant="outline" onClick={onOpenImport}>
                Import
              </Button>
            )}
            {onOpenExport && (
              <Button size="sm" variant="outline" onClick={onOpenExport}>
                Export
              </Button>
            )}
          </div>
        )}
      </div>

      <div className="mt-3 flex flex-wrap items-center gap-2">
        <Button
          size="sm"
          variant={includeProxies ? "default" : "outline"}
          onClick={() => onIncludeProxiesChange(!includeProxies)}
          data-testid="toggle-proxies"
        >
          {includeProxies ? "Showing A + Proxies" : "Showing Owned Only"}
        </Button>
        {totalMissingCopies > 0 && (
          <>
            <Button
              size="sm"
              variant={missingOnly ? "default" : "outline"}
              onClick={onToggleMissingOnly}
              data-testid="toggle-missing-only"
            >
              Missing Only
            </Button>
            {onAddMissingToWishlist && (
              <Button
                size="sm"
                variant="secondary"
                disabled={isAddingMissing}
                onClick={onAddMissingToWishlist}
                data-testid="add-all-missing-wishlist"
              >
                {isAddingMissing ? "Adding…" : `Add Missing (${totalMissingCopies})`}
              </Button>
            )}
          </>
        )}
      </div>

      {validationIssues && validationIssues.length > 0 && (
        <div
          className="mt-3 rounded border border-amber-500/40 bg-amber-950/30 p-2 text-xs text-amber-200 space-y-0.5"
          data-testid="sidebar-validation-issues"
        >
          <div className="font-semibold">Deck Rules ({validationIssues.length})</div>
          <ul className="list-disc pl-4 space-y-0.5 max-h-24 overflow-y-auto">
            {validationIssues.map((issue, idx) => (
              <li key={`${idx}-${issue}`}>{issue}</li>
            ))}
          </ul>
        </div>
      )}

      <ValueHistoryBox valueHistory={valueHistory} formatCurrency={formatCurrency} />
    </div>
  );
}
