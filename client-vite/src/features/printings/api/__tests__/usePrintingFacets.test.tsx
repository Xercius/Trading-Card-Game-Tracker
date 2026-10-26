import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { renderHook, waitFor } from "@testing-library/react";
import type { PropsWithChildren } from "react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import type { CardsFacetResponse } from "@/features/cards/api/list";
import type { CardFilters } from "@/features/cards/filters/useCardFilters";
import { usePrintingFacets } from "../usePrintingFacets";

const { fetchCardFacetsMock } = vi.hoisted(() => ({
  fetchCardFacetsMock: vi.fn(),
}));

vi.mock("@/features/cards/api/list", () => ({
  fetchCardFacets: fetchCardFacetsMock,
}));

function createWrapper() {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  });
  return ({ children }: PropsWithChildren) => (
    <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>
  );
}

describe("usePrintingFacets", () => {
  beforeEach(() => {
    fetchCardFacetsMock.mockReset();
  });

  it("requests facets with active filters and exposes database-wide values", async () => {
    fetchCardFacetsMock.mockResolvedValue({
      games: [{ value: "Star Wars Unlimited", count: 4 }],
      sets: [{ value: "Late Set", count: 1 }],
      rarities: [{ value: "Rare", count: 2 }],
      types: [{ value: "Leader Unit", count: 2 }],
    } satisfies CardsFacetResponse);
    const filters: CardFilters = {
      q: "droid",
      games: ["Star Wars Unlimited"],
      sets: [],
      rarities: ["Rare"],
      types: ["Leader Unit"],
      page: 1,
      pageSize: 60,
    };

    const { result } = renderHook(() => usePrintingFacets(filters), { wrapper: createWrapper() });

    await waitFor(() => expect(result.current.data?.sets).toEqual(["Late Set"]));
    expect(fetchCardFacetsMock).toHaveBeenCalledWith({
      games: ["Star Wars Unlimited"],
      sets: undefined,
      rarities: ["Rare"],
      types: ["Leader Unit"],
      q: "droid",
      printingSearch: true,
    });
  });
});
