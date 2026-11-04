import { useEffect, useState } from "react";
import { useUser } from "@/state/useUser";
import { useGame } from "@/state/useGame";
import MoxfieldCardSearch from "@/components/search/MoxfieldCardSearch";
import AddCardModal from "./AddCardModal";
import MassEntryModal from "./MassEntryModal";
import LineSparkline from "@/components/charts/LineSparkline";
import { useCollectionValueHistory } from "./useCollectionValueHistory";
import { latestValue } from "@/lib/valueHistory";
import { CollectionSetsTable } from "./components/CollectionSetsTable";
import type { PrintingListItem } from "@/features/printings/api/printings";

function formatCurrency(value: number | null): string {
  if (value == null || Number.isNaN(value)) return "—";
  return `$${value.toFixed(2)}`;
}

const EMPTY_VALUE_HISTORY_POINTS: { d: string; v: number | null }[] = [];

export default function CollectionPage() {
  const { userId } = useUser();
  const { activeGame } = useGame();

  const [addModalPrinting, setAddModalPrinting] = useState<PrintingListItem | null>(null);
  const [isAddModalOpen, setIsAddModalOpen] = useState(false);
  const [isMassEntryOpen, setIsMassEntryOpen] = useState(false);
  const [toastMessage, setToastMessage] = useState<string | null>(null);

  useEffect(() => {
    if (!toastMessage) return;
    const timer = setTimeout(() => setToastMessage(null), 3500);
    return () => clearTimeout(timer);
  }, [toastMessage]);

  const valueHistoryQuery = useCollectionValueHistory(undefined, !!userId);
  const valueHistoryPoints = valueHistoryQuery.data ?? EMPTY_VALUE_HISTORY_POINTS;
  const collectionLatestValue = latestValue(valueHistoryPoints);

  if (!userId) {
    return (
      <div className="p-6 text-sm text-zinc-400">
        Select a user to view their collection.
      </div>
    );
  }

  return (
    <div className="flex h-full flex-col gap-5 p-6 max-w-7xl mx-auto w-full">
      {toastMessage && (
        <div className="rounded-md bg-purple-900/30 border border-purple-500/30 px-4 py-2.5 text-sm text-purple-300 font-medium">
          {toastMessage}
        </div>
      )}

      {/* Top Action Bar: Moxfield Quick Add Search + Mass Entry Button */}
      <div className="flex flex-wrap items-center justify-between gap-4 rounded-xl border border-zinc-800 bg-zinc-900/80 p-4 shadow-sm backdrop-blur-sm">
        <div className="flex-1 min-w-[280px] max-w-md">
          <label className="text-xs font-semibold uppercase tracking-wider text-zinc-400 mb-1.5 block">
            Quick Add Card to Collection
          </label>
          <MoxfieldCardSearch
            onSelectPrinting={(printing) => {
              setAddModalPrinting(printing);
              setIsAddModalOpen(true);
            }}
          />
        </div>

        <div className="flex items-center gap-2">
          <button
            type="button"
            onClick={() => setIsMassEntryOpen(true)}
            className="flex items-center gap-2 rounded-lg bg-zinc-800 hover:bg-zinc-700 px-4 py-2 text-sm font-semibold text-zinc-100 border border-zinc-700 transition-colors shadow-sm"
          >
            <svg className="w-4 h-4 text-purple-400" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M3 10h18M3 14h18m-9-4v8m-7 0h14a2 2 0 002-2V6a2 2 0 00-2-2H5a2 2 0 00-2 2v12a2 2 0 002 2z" />
            </svg>
            Mass Entry (Set Spreadsheet)
          </button>
        </div>
      </div>

      {/* Collection Value History */}
      <section className="rounded-xl border border-zinc-800 bg-zinc-900/80 p-4 shadow-sm">
        <div className="flex items-center justify-between gap-4">
          <div>
            <h2 className="text-sm font-semibold text-zinc-200">Collection Value (Last 90 Days)</h2>
            {collectionLatestValue != null && (
              <p className="text-xs text-zinc-400 font-mono mt-0.5">
                Latest: <span className="text-emerald-400 font-bold">{formatCurrency(collectionLatestValue)}</span>
              </p>
            )}
          </div>
          <span className="text-xs text-zinc-500">Proxies excluded</span>
        </div>
        <div className="mt-2">
          {valueHistoryQuery.isLoading && (
            <p className="text-xs text-zinc-500">Loading value history…</p>
          )}
          {valueHistoryQuery.isError && (
            <p className="text-xs text-red-400">Failed to load value history.</p>
          )}
          {!valueHistoryQuery.isLoading &&
            !valueHistoryQuery.isError &&
            valueHistoryPoints.length === 0 && (
              <p className="text-xs text-zinc-500">No value data.</p>
            )}
          {valueHistoryPoints.length > 0 && (
            <LineSparkline
              points={valueHistoryPoints}
              ariaLabel="Collection value over time"
              height={70}
              className="h-20"
              stroke="#a855f7"
            />
          )}
        </div>
      </section>

      {/* Sets Overview Table */}
      <section className="space-y-2">
        <div className="flex items-center justify-between">
          <h2 className="text-base font-bold text-zinc-100 tracking-tight">
            Sets & Expansions
          </h2>
          <span className="text-xs text-zinc-400">
            Click any set to view all printings and manage quantities
          </span>
        </div>
        <CollectionSetsTable />
      </section>

      {/* Quick Add Modal */}
      {addModalPrinting && (
        <AddCardModal
          open={isAddModalOpen}
          onOpenChange={(open) => {
            setIsAddModalOpen(open);
            if (!open) setAddModalPrinting(null);
          }}
          initialPrinting={addModalPrinting}
          mode="collection"
          onSuccess={(msg) => setToastMessage(msg)}
        />
      )}

      {/* Mass Entry Modal */}
      <MassEntryModal
        open={isMassEntryOpen}
        onOpenChange={setIsMassEntryOpen}
        game={activeGame ?? undefined}
        onSuccess={(msg) => setToastMessage(msg)}
      />
    </div>
  );
}
