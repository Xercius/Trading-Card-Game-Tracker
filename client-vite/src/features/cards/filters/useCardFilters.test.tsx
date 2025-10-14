import { act } from "react-dom/test-utils";
import { createRoot } from "react-dom/client";
import { MemoryRouter, useSearchParams } from "react-router-dom";
import { describe, expect, it } from "vitest";
import { useCardFilters } from "./useCardFilters";
import type { CardFilters } from "./useCardFilters";

type ProbeState = {
  search: string;
  filters: CardFilters;
  queryKey: readonly [string, string, string, string, number, number, string];
  setFilters: ReturnType<typeof useCardFilters>["setFilters"];
};

let latest: ProbeState | undefined;

function FiltersProbe() {
  const hook = useCardFilters();
  const [params] = useSearchParams();
  latest = {
    search: params.toString(),
    filters: hook.filters,
    queryKey: hook.toQueryKey(),
    setFilters: hook.setFilters,
  };
  return null;
}

describe("useCardFilters", () => {
  it("parses and serializes filters from the URL", async () => {
    const container = document.createElement("div");
    const root = createRoot(container);

    await act(async () => {
      root.render(
        <MemoryRouter initialEntries={["/cards?game=Magic,Lorcana&set=Rise&rarity=R,U&q=bolt&page=2&pageSize=30"]}>
          <FiltersProbe />
        </MemoryRouter>
      );
    });

    expect(latest?.filters.games).toEqual(["Magic", "Lorcana"]);
    expect(latest?.filters.sets).toEqual(["Rise"]);
    expect(latest?.filters.rarities).toEqual(["R", "U"]);
    expect(latest?.filters.q).toBe("bolt");
    expect(latest?.filters.page).toBe(2);
    expect(latest?.filters.pageSize).toBe(30);
    // URL params are encoded, so use decoded comparison
    const searchStr = latest?.search ?? "";
    expect(decodeURIComponent(searchStr)).toContain("game=Magic,Lorcana");
    expect(decodeURIComponent(searchStr)).toContain("set=Rise");
    expect(decodeURIComponent(searchStr)).toContain("rarity=R,U");
    expect(decodeURIComponent(searchStr)).toContain("q=bolt");
    expect(searchStr).toContain("page=2");
    expect(searchStr).toContain("pageSize=30");

    await act(async () => {
      latest?.setFilters((prev) => prev);
    });

    const finalSearchStr = latest?.search ?? "";
    expect(decodeURIComponent(finalSearchStr)).toContain("game=Magic,Lorcana");
    expect(latest?.queryKey).toEqual(["bolt", "Magic|Lorcana", "Rise", "R|U", 2, 30, ""]);

    await act(async () => {
      root.unmount();
    });

    latest = undefined;
  });
});
