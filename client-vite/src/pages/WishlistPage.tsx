import { useEffect, useMemo, useRef, useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useUser } from "@/state/useUser";
import http from "@/lib/http";
import { useQueryState } from "@/hooks/useQueryState";
import {
  collectionKeys,
  type CollectionItem,
  type Paged as CollectionPaged,
} from "@/features/collection/api";

const DEFAULT_PAGE_SIZE = 50;

type WishlistItemDto = {
  cardPrintingId: number;
  quantityWanted: number;
  cardId: number;
  cardName: string;
  game: string;
  set: string;
  number: string;
  rarity: string;
  style: string;
  imageUrl: string | null;
};

type Paged<T> = {
  items: T[];
  total: number;
  page: number;
  pageSize: number;
};

type MoveToCollectionResponse = {
  printingId: number;
  wantedAfter: number;
  ownedAfter: number;
  proxyAfter: number;
  availability: number;
  availabilityWithProxies: number;
};

type MoveVariables = {
  printingId: number;
  quantity: number;
  useProxy?: boolean;
};

type ToastMessage = {
  type: "success" | "error" | "info";
  message: string;
};

function promptForQuantity(item: WishlistItemDto): number | null {
  const defaultQty = Math.max(1, item.quantityWanted);
  const input = window.prompt(`Move how many copies of ${item.cardName}?`, String(defaultQty));
  if (input == null) return null;
  const parsed = Number(input);
  if (!Number.isFinite(parsed)) return null;
  const normalized = Math.trunc(parsed);
  if (normalized <= 0) return null;
  return normalized;
}

function formatMovedMessage(count: number) {
  if (count <= 0) return "No copies moved.";
  return `Moved ${count} card${count === 1 ? "" : "s"} to collection.`;
}

export default function WishlistPage() {
  const { userId } = useUser();
  const [q] = useQueryState("q", "");
  const [gameCsv] = useQueryState("game", "");
  const [pageParamRaw, setPageParam] = useQueryState("page", "1");
  const [pageSizeParamRaw] = useQueryState("pageSize", String(DEFAULT_PAGE_SIZE));

  const parsedPage = Number(pageParamRaw);
  const parsedPageSize = Number(pageSizeParamRaw);
  const page = Number.isFinite(parsedPage) && parsedPage > 0 ? Math.floor(parsedPage) : 1;
  const pageSize =
    Number.isFinite(parsedPageSize) && parsedPageSize > 0
      ? Math.floor(parsedPageSize)
      : DEFAULT_PAGE_SIZE;

  const previousFiltersRef = useRef({ q, gameCsv });
  const shouldResetPage =
    pageParamRaw !== "1" &&
    (previousFiltersRef.current.q !== q || previousFiltersRef.current.gameCsv !== gameCsv);

  useEffect(() => {
    if (shouldResetPage) {
      setPageParam("1");
    }
    previousFiltersRef.current = { q, gameCsv };
  }, [shouldResetPage, setPageParam, q, gameCsv]);

  const updatePage = (nextPage: number) => {
    const safeNext = nextPage < 1 ? 1 : nextPage;
    setPageParam(String(safeNext));
  };

  const wishlistQueryKey = useMemo(
    () => ["wishlist", userId, page, pageSize, q, gameCsv] as const,
    [userId, page, pageSize, q, gameCsv]
  );

  const queryClient = useQueryClient();
  const [selectedIds, setSelectedIds] = useState<Set<number>>(new Set());
  const [toast, setToast] = useState<ToastMessage | null>(null);
  const [isBulkRunning, setIsBulkRunning] = useState(false);

  useEffect(() => {
    if (!toast) return;
    const timer = window.setTimeout(() => setToast(null), 3200);
    return () => window.clearTimeout(timer);
  }, [toast]);

  const { data, isLoading, error } = useQuery<Paged<WishlistItemDto>>({
    queryKey: wishlistQueryKey,
    queryFn: async () => {
      if (!userId) throw new Error("User not selected");
      const res = await http.get<Paged<WishlistItemDto>>(`user/${userId}/wishlist`, {
        params: { page, pageSize, name: q || undefined, game: gameCsv },
      });
      return res.data;
    },
    enabled: !!userId && !shouldResetPage,
  });

  const items = data?.items ?? [];
  const total = data?.total ?? 0;
  const canGoPrev = page > 1;
  const canGoNext = items.length === pageSize && page * pageSize < total;

  const moveMutation = useMutation({
    mutationFn: async (variables: MoveVariables) => {
      const response = await http.post<MoveToCollectionResponse>("wishlist/move-to-collection", {
        cardPrintingId: variables.printingId,
        quantity: variables.quantity,
        useProxy: variables.useProxy ?? false,
      });
      return response.data;
    },
    onSuccess: (snapshot, variables) => {
      queryClient.setQueryData<Paged<WishlistItemDto>>(wishlistQueryKey, (existing) => {
        if (!existing) return existing;

        const present = existing.items.some((item) => item.cardPrintingId === snapshot.printingId);
        if (!present) return existing;

        if (snapshot.wantedAfter <= 0) {
          const nextItems = existing.items.filter(
            (item) => item.cardPrintingId !== snapshot.printingId
          );
          const nextTotal = Math.max(0, existing.total - 1);
          return { ...existing, items: nextItems, total: nextTotal } as Paged<WishlistItemDto>;
        }

        const nextItems = existing.items.map((item) =>
          item.cardPrintingId === snapshot.printingId
            ? { ...item, quantityWanted: snapshot.wantedAfter }
            : item
        );
        return { ...existing, items: nextItems } as Paged<WishlistItemDto>;
      });

      const collectionQueries = queryClient.getQueriesData<CollectionPaged<CollectionItem>>(
        collectionKeys.all
      );
      for (const [key, value] of collectionQueries) {
        if (!value) continue;
        const idx = value.items.findIndex((item) => item.cardPrintingId === snapshot.printingId);
        if (idx < 0) continue;

        const nextItems = value.items.map((item) =>
          item.cardPrintingId === snapshot.printingId
            ? {
                ...item,
                quantityOwned: snapshot.ownedAfter,
                quantityProxyOwned: snapshot.proxyAfter,
                availability: snapshot.availability,
                availabilityWithProxies: snapshot.availabilityWithProxies,
              }
            : item
        );
        queryClient.setQueryData(key, { ...value, items: nextItems });
      }

      setSelectedIds((prev) => {
        if (!prev.has(snapshot.printingId)) return prev;
        const next = new Set(prev);
        next.delete(snapshot.printingId);
        return next;
      });
    },
  });

  const handleMove = async (item: WishlistItemDto) => {
    if (item.quantityWanted <= 0) {
      setToast({ type: "info", message: "Nothing left to move." });
      return;
    }

    const quantity = promptForQuantity(item);
    if (quantity == null) {
      setToast({ type: "error", message: "Quantity must be a positive number." });
      return;
    }

    try {
      const snapshot = await moveMutation.mutateAsync({
        printingId: item.cardPrintingId,
        quantity,
      });
      const moved = Math.max(0, item.quantityWanted - snapshot.wantedAfter);
      setToast({ type: moved > 0 ? "success" : "info", message: formatMovedMessage(moved) });
    } catch (err) {
      // eslint-disable-next-line no-console
      console.error("[WishlistPage] Failed to move to collection", err);
      setToast({ type: "error", message: "Failed to move card to collection." });
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

  const handleBulkMove = async () => {
    if (selectedIds.size === 0) return;
    const selectedItems = items.filter((item) => selectedIds.has(item.cardPrintingId));
    if (selectedItems.length === 0) return;

    setIsBulkRunning(true);
    try {
      const results = await Promise.allSettled(
        selectedItems.map(async (item) => {
          if (item.quantityWanted <= 0) {
            throw new Error("Nothing left to move.");
          }
          const quantity = promptForQuantity(item);
          if (quantity == null) throw new Error("Quantity must be a positive number.");

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
        const item = selectedItems[index];
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
    } finally {
      setIsBulkRunning(false);
    }
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
  if (items.length === 0)
    return (
      <div className="p-4">
        {toast && (
          <div className={`mb-3 rounded px-3 py-2 text-sm ${toastClass}`}>{toast.message}</div>
        )}
        No wishlist items found
      </div>
    );

  return (
    <div className="p-4">
      {toast && (
        <div className={`mb-3 rounded px-3 py-2 text-sm ${toastClass}`}>{toast.message}</div>
      )}
      <div className="mb-2 text-sm text-gray-500">
        Showing {items.length} of {total} items
        <div className="mt-2 flex flex-wrap items-center gap-2">
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
            onClick={handleBulkMove}
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
            className="flex items-center justify-between gap-3 rounded border border-gray-200 p-3"
          >
            <label className="flex grow items-center gap-3">
              <input
                type="checkbox"
                checked={selectedIds.has(item.cardPrintingId)}
                onChange={() => handleToggle(item.cardPrintingId)}
              />
              <span>
                {item.game} — {item.cardName} · want {item.quantityWanted}
              </span>
            </label>
            <button
              className="rounded bg-indigo-600 px-3 py-1 text-sm font-medium text-white disabled:opacity-50"
              type="button"
              onClick={() => handleMove(item)}
              disabled={moveMutation.isPending}
            >
              Move to collection
            </button>
          </li>
        ))}
      </ul>
    </div>
  );
}
