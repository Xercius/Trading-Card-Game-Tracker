import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { act, renderHook, waitFor } from "@testing-library/react";
import type { PropsWithChildren } from "react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import type { CardFilters } from "@/features/cards/filters/useCardFilters";
import { usePrintings } from "../usePrintings";
import type { PrintingListItem } from "../printings";

const { fetchPrintingsMock } = vi.hoisted(() => ({
  fetchPrintingsMock: vi.fn(),
}));

vi.mock("../printings", async (importOriginal) => ({
  ...(await importOriginal<typeof import("../printings")>()),
  fetchPrintings: fetchPrintingsMock,
}));

const filters: CardFilters = {
  q: "droid",
  games: ["Star Wars Unlimited"],
  sets: ["SOR"],
  rarities: ["Rare"],
  types: ["Leader Unit"],
  arenas: [],
  aspects: [],
  traits: [],
  page: 1,
  pageSize: 2,
};

const printing = (printingId: string): PrintingListItem => ({
  printingId,
  cardId: `card-${printingId}`,
  cardName: `Card ${printingId}`,
  game: "Star Wars Unlimited",
  setName: "SOR",
  number: printingId,
  rarity: "Rare",
});

function createWrapper() {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  });
  return ({ children }: PropsWithChildren) => (
    <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>
  );
}

describe("usePrintings pagination", () => {
  beforeEach(() => {
    fetchPrintingsMock.mockReset();
  });

  it("preserves filters across pages and removes duplicate printing IDs", async () => {
    fetchPrintingsMock
      .mockResolvedValueOnce([printing("1"), printing("2")])
      .mockResolvedValueOnce([printing("2"), printing("3")])
      .mockResolvedValueOnce([printing("4")]);

    const { result } = renderHook(() => usePrintings(filters), { wrapper: createWrapper() });

    await waitFor(() => expect(result.current.data).toHaveLength(2));
    await act(async () => {
      await result.current.fetchNextPage();
    });

    await waitFor(() => expect(result.current.data).toHaveLength(3));
    await act(async () => {
      await result.current.fetchNextPage();
    });

    await waitFor(() => expect(result.current.hasNextPage).toBe(false));
    expect(result.current.data?.map(({ printingId }) => printingId)).toEqual(["1", "2", "3", "4"]);
    expect(fetchPrintingsMock).toHaveBeenNthCalledWith(1, {
      q: "droid",
      game: ["Star Wars Unlimited"],
      set: ["SOR"],
      rarity: ["Rare"],
      types: ["Leader Unit"],
      pageSize: 2,
      page: 1,
      uniqueCards: true,
    });
    expect(fetchPrintingsMock).toHaveBeenNthCalledWith(2, {
      q: "droid",
      game: ["Star Wars Unlimited"],
      set: ["SOR"],
      rarity: ["Rare"],
      types: ["Leader Unit"],
      pageSize: 2,
      page: 2,
      uniqueCards: true,
    });
    expect(fetchPrintingsMock).toHaveBeenNthCalledWith(3, {
      q: "droid",
      game: ["Star Wars Unlimited"],
      set: ["SOR"],
      rarity: ["Rare"],
      types: ["Leader Unit"],
      pageSize: 2,
      page: 3,
      uniqueCards: true,
    });
  });

  it("starts a new result set at the requested first page after filters change", async () => {
    fetchPrintingsMock.mockResolvedValue([printing("new")]);
    const { result, rerender } = renderHook(
      ({ activeFilters }: { activeFilters: CardFilters }) => usePrintings(activeFilters),
      { initialProps: { activeFilters: filters }, wrapper: createWrapper() }
    );

    await waitFor(() => expect(result.current.data).toHaveLength(1));
    rerender({ activeFilters: { ...filters, q: "new query", page: 1 } });

    await waitFor(() => expect(fetchPrintingsMock).toHaveBeenCalledTimes(2));
    expect(fetchPrintingsMock).toHaveBeenLastCalledWith(
      expect.objectContaining({ q: "new query", page: 1 })
    );
  });
});
