import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render, screen, waitFor } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { describe, expect, it, vi } from "vitest";

const { fetchCardFacetsMock } = vi.hoisted(() => ({
  fetchCardFacetsMock: vi.fn(),
}));

vi.mock("../../api", () => ({
  fetchCardFacets: fetchCardFacetsMock,
}));

import FiltersRail from "../FiltersRail";

describe("FiltersRail", () => {
  it("canonicalizes case-insensitive URL selections to facet values", async () => {
    fetchCardFacetsMock.mockResolvedValue({
      games: [],
      sets: [],
      rarities: [],
      types: [],
      arenas: [{ value: "Ground", count: 1 }],
      aspects: [{ value: "Heroism", count: 1 }],
      traits: [{ value: "Jedi", count: 1 }],
    });
    const queryClient = new QueryClient({
      defaultOptions: { queries: { retry: false } },
    });

    render(
      <QueryClientProvider client={queryClient}>
        <MemoryRouter initialEntries={["/cards?arena=ground&aspect=heroism&trait=jedi"]}>
          <FiltersRail />
        </MemoryRouter>
      </QueryClientProvider>
    );

    await waitFor(() => {
      expect(screen.getByRole("checkbox", { name: "Ground" })).toHaveProperty("checked", true);
      expect(screen.getByRole("checkbox", { name: "Heroism" })).toHaveProperty("checked", true);
      expect(screen.getByRole("checkbox", { name: "Jedi" })).toHaveProperty("checked", true);
    });

    queryClient.clear();
  });
});
