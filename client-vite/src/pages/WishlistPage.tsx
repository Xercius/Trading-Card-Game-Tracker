import { useEffect, useMemo, useRef, useState } from "react";
import { useUser } from "@/state/useUser";
import { useGame } from "@/state/useGame";
import { useQueryState } from "@/hooks/useQueryState";
import { DEFAULT_PAGE_SIZE } from "@/constants";
import { queryKeys } from "@/lib/queryKeys";
import MoxfieldCardSearch from "@/components/search/MoxfieldCardSearch";
import AddCardModal from "@/features/collection/AddCardModal";
import EditCardModal from "@/features/collection/EditCardModal";
import type { PrintingListItem } from "@/features/printings/api/printings";
import {
  useMoveToCollectionMutation,
  useWishlistQuery,
  type WishlistItemDto,
} from "@/features/wishlist/api";
import { MoveToCollectionDialog } from "@/features/wishlist/MoveToCollectionDialog";

type ToastMessage = {
  type: "success" | "error" | "info";
  message: string;
};

function formatMovedMessage(count: number) {
  if (count <= 0) return "No copies moved.";
  return `Moved ${count} card${count === 1 ? "" : "s"} to collection.`;
}

export default function WishlistPage() {
  const { userId } = useUser();
  const [q] = useQueryState("q", "");
  const { activeGame } = useGame();
  const [pageParamRaw, setPageParam] = useQueryState("page", "1");
  const [pageSizeParamRaw] = useQueryState("pageSize", String(DEFAULT_PAGE_SIZE));

  const parsedPage = Number(pageParamRaw);
  const parsedPageSize = Number(pageSizeParamRaw);
  const page = Number.isFinite(parsedPage) && parsedPage > 0 ? Math.floor(parsedPage) : 1;
  const pageSize =
    Number.isFinite(parsedPageSize) && parsedPageSize > 0
      ? Math.floor(parsedPageSize)
      : DEFAULT_PAGE_SIZE;

  const previousFiltersRef = useRef({ q, game: activeGame });
  const shouldResetPage =
    pageParamRaw !== "1" &&
    (previousFiltersRef.current.q !== q || previousFiltersRef.current.game !== activeGame);

  useEffect(() => {
    if (shouldResetPage) {
      setPageParam("1");
    }
    previousFiltersRef.current = { q, game: activeGame };
  }, [shouldResetPage, setPageParam, q, activeGame]);

  const updatePage = (nextPage: number) => {
    const safeNext = nextPage < 1 ? 1 : nextPage;
    setPageParam(String(safeNext));
  };

  const wishlistQueryKey = useMemo(
    () => queryKeys.wishlist.list(userId, page, pageSize, q, activeGame),
    [userId, page, pageSize, q, activeGame]
  );

  const [selectedIds, setSelectedIds] = useState<Set<number>>(new Set());
  const [toast, setToast] = useState<ToastMessage | null>(null);
  const [isBulkRunning, setIsBulkRunning] = useState(false);

  const [addModalPrinting, setAddModalPrinting] = useState<PrintingListItem | null>(null);
  const [isAddModalOpen, setIsAddModalOpen] = useState(false);
  const [editModalItem, setEditModalItem] = useState<WishlistItemDto | null>(null);
  const [isEditModalOpen, setIsEditModalOpen] = useState(false);
  const [moveDialogItems, setMoveDialogItems] = useState<WishlistItemDto[] | null>(null);

  useEffect(() => {
    if (!toast) return;
    const timer = window.setTimeout(() => setToast(null), 3200);
    return () => window.clearTimeout(timer);
  }, [toast]);

  const { data, isLoading, error } = useWishlistQuery({
    userId,
    page,
    pageSize,
    q,
    game: activeGame,
    enabled: !shouldResetPage,
  });

  const moveMutation = useMoveToCollectionMutation(wishlistQueryKey, (printingId) => {
    setSelectedIds((prev) => {
      if (!prev.has(printingId)) return prev;
      const next = new Set(prev);
      next.delete(printingId);
      return next;
    });
  });

  const items = data?.items ?? [];
  const total = data?.total ?? 0;
  const canGoPrev = page > 1;
  const canGoNext = items.length === pageSize && page * pageSize < total;

  const openSingleMoveDialog = (item: WishlistItemDto) => {
    if (item.quantityWanted <= 0) {
      setToast({ type: "info", message: "Nothing left to move." });
      return;
    }
    setMoveDialogItems([item]);
  };

  const openBulkMoveDialog = () => {
    if (selectedIds.size === 0) return;
    const selectedItems = items.filter((item) => selectedIds.has(item.cardPrintingId));
    if (selectedItems.length === 0) return;
    setMoveDialogItems(selectedItems);
  };

  const handleConfirmMove = async (quantities: Map<number, number>) => {
    const targetItems = moveDialogItems ?? [];
    if (targetItems.length === 0) return;

    if (targetItems.length === 1) {
      const item = targetItems[0];
      const quantity = quantities.get(item.cardPrintingId) ?? 0;
      try {
        const snapshot = await moveMutation.mutateAsync({
          printingId: item.cardPrintingId,
          quantity,
        });
        const moved = Math.max(0, item.quantityWanted - snapshot.wantedAfter);
        setToast({ type: moved > 0 ? "success" : "info", message: formatMovedMessage(moved) });
        setMoveDialogItems(null);
      } catch {
        setToast({ type: "error", message: "Failed to move card to collection." });
      }
      return;
    }

    setIsBulkRunning(true);
    try {
      const results = await Promise.allSettled(
        targetItems.map(async (item) => {
          if (item.quantityWanted <= 0) {
            throw new Error("Nothing left to move.");
          }
          const quantity = quantities.get(item.cardPrintingId);
          if (!quantity || quantity <= 0) throw new Error("Quantity must be a positive number.");

          const snapshot = await moveMutation.mutateAsync({
            printingId: item.cardPrintingId,
            quantity,
          });
          return { item, snapshot };
        })
      );

      const successes: string[] = [];
      const failures: string[] = [];

      results.forEach((result, index) => {
        const item = targetItems[index];
        if (result.status === "fulfilled") {
          const moved = Math.max(0, item.quantityWanted - result.value.snapshot.wantedAfter);
          successes.push(`${item.cardName} (${moved})`);
        } else {
          const reason = result.reason instanceof Error ? result.reason.message : "Failed";
          failures.push(`${item.cardName}: ${reason}`);
        }
      });

      if (failures.length > 0) {
        const prefix = successes.length > 0 ? `Moved ${successes.length} item(s). ` : "";
        setToast({ type: "error", message: `${prefix}Failed: ${failures.join(", ")}` });
      } else if (successes.length > 0) {
        setToast({ type: "success", message: `Moved ${successes.length} item(s) to collection.` });
      }
      setMoveDialogItems(null);
    } finally {
      setIsBulkRunning(false);
    }
  };

  const handleToggle = (printingId: number) => {
    setSelectedIds((prev) => {
      const next = new Set(prev);
      if (next.has(printingId)) next.delete(printingId);
      else next.add(printingId);
      return next;
    });
  };

  const toastClass = (() => {
    switch (toast?.type) {
      case "success":
        return "bg-green-100 text-green-800";
      case "error":
        return "bg-red-100 text-red-700";
      case "info":
        return "bg-blue-100 text-blue-700";
      default:
        return "";
    }
  })();

  if (isLoading) return <div className="p-4">Loading…</div>;
  if (error) return <div className="p-4 text-red-500">Error loading wishlist</div>;

  return (
    <div className="p-4 max-w-5xl mx-auto space-y-4">
      {toast && (
        <div className={`mb-3 rounded px-3 py-2 text-sm ${toastClass}`}>{toast.message}</div>
      )}

      {/* Top Action Bar: Moxfield Quick Add to Wishlist Search */}
      <div className="rounded-lg border border-gray-200 bg-white dark:bg-card dark:border-border p-4 shadow-sm">
        <label className="text-xs font-semibold uppercase text-gray-500 mb-1.5 block">
          Quick Add Card to Wishlist
        </label>
        <MoxfieldCardSearch
          placeholder="Search card to add to wishlist… (type name, set, or #)"
          onSelectPrinting={(printing) => {
            setAddModalPrinting(printing);
            setIsAddModalOpen(true);
          }}
        />
      </div>

      {items.length === 0 ? (
        <div className="rounded-lg border border-dashed border-gray-300 p-8 text-center text-sm text-gray-500">
          No wishlist items found. Use the search box above to add cards to your wishlist!
        </div>
      ) : (
        <>
          <div className="mb-2 text-sm text-gray-500 flex flex-wrap items-center justify-between gap-2">
            <div>
              Showing {items.length} of {total} items
            </div>
            <div className="flex flex-wrap items-center gap-2">
              <button
                className="rounded border px-2 py-1 disabled:opacity-50"
                onClick={() => canGoPrev && updatePage(page - 1)}
                disabled={!canGoPrev}
                type="button"
              >
                Prev
              </button>
              <button
                className="rounded border px-2 py-1 disabled:opacity-50"
                onClick={() => canGoNext && updatePage(page + 1)}
                disabled={!canGoNext}
                type="button"
              >
                Next
              </button>
              <button
                className="rounded border px-2 py-1 disabled:opacity-50"
                onClick={openBulkMoveDialog}
                disabled={selectedIds.size === 0 || moveMutation.isPending || isBulkRunning}
                type="button"
              >
                Move selected
              </button>
            </div>
          </div>
          <ul className="space-y-2">
            {items.map((item) => (
              <li
                key={item.cardPrintingId}
                className="flex items-center justify-between gap-3 rounded border border-gray-200 dark:border-border p-3 hover:bg-gray-50 dark:hover:bg-muted/30 transition-colors"
              >
                <label className="flex grow items-center gap-3 cursor-pointer">
                  <input
                    type="checkbox"
                    checked={selectedIds.has(item.cardPrintingId)}
                    onChange={() => handleToggle(item.cardPrintingId)}
                  />
                  <span>
                    <span className="font-medium">{item.cardName}</span>
                    <span className="text-xs text-gray-500 ml-2">
                      {item.set} #{item.number} · want {item.quantityWanted}
                    </span>
                  </span>
                </label>
                <button
                  type="button"
                  className="rounded border px-2 py-1 text-sm hover:bg-muted"
                  onClick={() => {
                    setEditModalItem(item);
                    setIsEditModalOpen(true);
                  }}
                  aria-label={`Edit ${item.cardName} wishlist entry`}
                >
                  Edit
                </button>
                <button
                  className="rounded bg-indigo-600 px-3 py-1 text-sm font-medium text-white hover:bg-indigo-700 disabled:opacity-50 transition-colors"
                  type="button"
                  onClick={() => openSingleMoveDialog(item)}
                  disabled={moveMutation.isPending}
                >
                  Move to collection
                </button>
              </li>
            ))}
          </ul>
        </>
      )}

      {isAddModalOpen && (
        <AddCardModal
          open={isAddModalOpen}
          onOpenChange={setIsAddModalOpen}
          initialPrinting={addModalPrinting}
          mode="wishlist"
          onSuccess={(msg) => setToast({ type: "success", message: msg })}
        />
      )}

      {isEditModalOpen && (
        <EditCardModal
          open={isEditModalOpen}
          onOpenChange={setIsEditModalOpen}
          item={editModalItem}
          mode="wishlist"
          onSuccess={(msg) => setToast({ type: "success", message: msg })}
        />
      )}

      <MoveToCollectionDialog
        open={moveDialogItems !== null}
        onOpenChange={(open) => {
          if (!open) setMoveDialogItems(null);
        }}
        items={moveDialogItems ?? []}
        isPending={moveMutation.isPending || isBulkRunning}
        onConfirm={handleConfirmMove}
      />
    </div>
  );
}
