import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { renderHook, waitFor } from "@testing-library/react";
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
  aspectMethod: "or",
  traits: [],
  traitMethod: "or",
  keywords: [],
  keywordMethod: "or",
  page: 1,
  pageSize: 2,
  stages: ["Stage 1"],
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

  it("queries the requested page with filters", async () => {
    fetchPrintingsMock.mockResolvedValueOnce({
      items: [printing("1"), printing("2")],
      total: 4,
    });

    const { result } = renderHook(() => usePrintings(filters), { wrapper: createWrapper() });

    await waitFor(() => expect(result.current.data?.items).toHaveLength(2));
    expect(result.current.data?.total).toBe(4);
    expect(fetchPrintingsMock).toHaveBeenCalledWith({
      q: "droid",
      game: ["Star Wars Unlimited"],
      set: ["SOR"],
      rarity: ["Rare"],
      types: ["Leader Unit"],
      stages: ["Stage 1"],
      pageSize: 2,
      page: 1,
      uniqueCards: true,
    });
  });

  it("passes selected Pokemon HP values to the printing query", async () => {
    fetchPrintingsMock.mockResolvedValueOnce({ items: [], total: 0 });

    const { result } = renderHook(
      () => usePrintings({ ...filters, games: ["Pokemon TCG"], hps: ["100"] }),
      { wrapper: createWrapper() }
    );

    await waitFor(() => expect(result.current.data?.total).toBe(0));
    expect(fetchPrintingsMock).toHaveBeenCalledWith(
      expect.objectContaining({ game: ["Pokemon TCG"], hps: ["100"] })
    );
  });

  it("starts a new result set at the requested first page after filters change", async () => {
    fetchPrintingsMock.mockResolvedValue([printing("new")]);
    const { result, rerender } = renderHook(
      ({ activeFilters }: { activeFilters: CardFilters }) => usePrintings(activeFilters),
      { initialProps: { activeFilters: filters }, wrapper: createWrapper() }
    );

    await waitFor(() => expect(result.current.data?.items).toHaveLength(1));
    rerender({ activeFilters: { ...filters, q: "new query", page: 1 } });

    await waitFor(() => expect(fetchPrintingsMock).toHaveBeenCalledTimes(2));
    expect(fetchPrintingsMock).toHaveBeenLastCalledWith(
      expect.objectContaining({ q: "new query", page: 1 })
    );
  });

  it("includes the deck aspect budget in the server request and cache key", async () => {
    fetchPrintingsMock.mockResolvedValue({ items: [printing("1")], total: 1 });
    const initialProps: { budget: string[] | undefined } = {
      budget: ["Command", "Command", "Villainy"],
    };
    const { result, rerender } = renderHook(
      ({ budget }: { budget: string[] | undefined }) => usePrintings(filters, budget),
      { initialProps, wrapper: createWrapper() }
    );
    await waitFor(() => expect(result.current.data?.total).toBe(1));
    expect(fetchPrintingsMock).toHaveBeenLastCalledWith(
      expect.objectContaining({ availableAspects: ["Command", "Command", "Villainy"] })
    );
    rerender({ budget: [] });
    await waitFor(() =>
      expect(fetchPrintingsMock).toHaveBeenLastCalledWith(
        expect.objectContaining({ availableAspects: [] })
      )
    );
    rerender({ budget: undefined });
    await waitFor(() => expect(fetchPrintingsMock).toHaveBeenCalledTimes(3));
    expect(fetchPrintingsMock.mock.lastCall?.[0]).not.toHaveProperty("availableAspects");
  });
});
