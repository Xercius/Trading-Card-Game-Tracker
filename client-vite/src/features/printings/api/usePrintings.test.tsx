import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { renderHook, waitFor } from "@testing-library/react";
import type { ReactNode } from "react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import type { CardFilters } from "@/features/cards/filters/useCardFilters";

const { fetchPrintingsMock } = vi.hoisted(() => ({
  fetchPrintingsMock: vi.fn().mockResolvedValue([]),
}));

vi.mock("./printings", () => ({
  fetchPrintings: (query: unknown) => fetchPrintingsMock(query),
}));

import { usePrintings } from "./usePrintings";

describe("usePrintings", () => {
  beforeEach(() => {
    fetchPrintingsMock.mockClear();
  });

  it("requests unique card results", async () => {
    const queryClient = new QueryClient({
      defaultOptions: { queries: { retry: false } },
    });
    const filters: CardFilters = {
      q: "",
      games: [],
      sets: [],
      rarities: [],
      page: 1,
      pageSize: 60,
      sort: undefined,
    };
    const wrapper = ({ children }: { children: ReactNode }) => (
      <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>
    );

    const { unmount } = renderHook(() => usePrintings(filters), { wrapper });

    await waitFor(() => {
      expect(fetchPrintingsMock).toHaveBeenCalledWith(
        expect.objectContaining({ uniqueCards: true })
      );
    });

    unmount();
    queryClient.clear();
  });
});
