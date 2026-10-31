import { act } from "react-dom/test-utils";
import { createRoot } from "react-dom/client";
import { MemoryRouter, useSearchParams } from "react-router-dom";
import { describe, expect, it } from "vitest";
import { useCardFilters } from "./useCardFilters";
import type { CardFilters } from "./useCardFilters";

type ProbeState = {
  search: string;
  filters: CardFilters;
  queryKey: ReturnType<ReturnType<typeof useCardFilters>["toQueryKey"]>;
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
        <MemoryRouter
          initialEntries={[
            "/cards?game=Magic,Lorcana&set=Rise&rarity=R,U&type=Leader,Leader%20Unit&arena=Ground&aspect=Heroism,Command&trait=Force,Rebel&q=bolt&page=2&pageSize=30",
          ]}
        >
          <FiltersProbe />
        </MemoryRouter>
      );
    });

    expect(latest?.filters.games).toEqual(["Magic", "Lorcana"]);
    expect(latest?.filters.sets).toEqual(["Rise"]);
    expect(latest?.filters.rarities).toEqual(["R", "U"]);
    expect(latest?.filters.types).toEqual(["Leader", "Leader Unit"]);
    expect(latest?.filters.arenas).toEqual(["Ground"]);
    expect(latest?.filters.aspects).toEqual(["Heroism", "Command"]);
    expect(latest?.filters.aspectMethod).toBe("or");
    expect(latest?.filters.traits).toEqual(["Force", "Rebel"]);
    expect(latest?.filters.q).toBe("bolt");
    expect(latest?.filters.page).toBe(2);
    expect(latest?.filters.pageSize).toBe(30);
    // URL params are encoded, so use decoded comparison
    const searchStr = latest?.search ?? "";
    expect(decodeURIComponent(searchStr)).toContain("game=Magic,Lorcana");
    expect(decodeURIComponent(searchStr)).toContain("set=Rise");
    expect(decodeURIComponent(searchStr)).toContain("rarity=R,U");
    expect(new URLSearchParams(searchStr).get("type")).toBe("Leader,Leader Unit");
    expect(new URLSearchParams(searchStr).get("arena")).toBe("Ground");
    expect(new URLSearchParams(searchStr).get("aspect")).toBe("Heroism,Command");
    expect(new URLSearchParams(searchStr).get("trait")).toBe("Force,Rebel");
    expect(decodeURIComponent(searchStr)).toContain("q=bolt");
    expect(searchStr).toContain("page=2");
    expect(searchStr).toContain("pageSize=30");

    await act(async () => {
      latest?.setFilters((prev) => prev);
    });

    const finalSearchStr = latest?.search ?? "";
    expect(decodeURIComponent(finalSearchStr)).toContain("game=Magic,Lorcana");
    expect(latest?.queryKey).toEqual([
      "bolt",
      "Magic|Lorcana",
      "Rise",
      "R|U",
      "Leader|Leader Unit",
      "Ground",
      "Heroism|Command",
      "Force|Rebel",
      2,
      30,
      "",
      "or",
      "or",
      "",
      "or",
    ]);

    await act(async () => {
      root.unmount();
    });

    latest = undefined;
  });

  it("preserves aspect multiplicity and parses aspectMethod", async () => {
    const container = document.createElement("div");
    const root = createRoot(container);

    await act(async () => {
      root.render(
        <MemoryRouter initialEntries={["/cards?aspect=Aggression,Aggression&aspectMethod=exclude"]}>
          <FiltersProbe />
        </MemoryRouter>
      );
    });

    expect(latest?.filters.aspects).toEqual(["Aggression", "Aggression"]);
    expect(latest?.filters.aspectMethod).toBe("exclude");
    expect(latest?.search).toContain("aspect=Aggression%2CAggression");
    expect(latest?.search).toContain("aspectMethod=exclude");
  });

  it("parses traitMethod and serializes correctly", async () => {
    const container = document.createElement("div");
    const root = createRoot(container);

    await act(async () => {
      root.render(
        <MemoryRouter initialEntries={["/cards?trait=Force,Imperial&traitMethod=and"]}>
          <FiltersProbe />
        </MemoryRouter>
      );
    });

    expect(latest?.filters.traits).toEqual(["Force", "Imperial"]);
    expect(latest?.filters.traitMethod).toBe("and");
    expect(latest?.search).toContain("trait=Force%2CImperial");
    expect(latest?.search).toContain("traitMethod=and");

    await act(async () => {
      root.unmount();
    });

    latest = undefined;
  });

  it("parses keywordMethod and serializes correctly", async () => {
    const container = document.createElement("div");
    const root = createRoot(container);

    await act(async () => {
      root.render(
        <MemoryRouter initialEntries={["/cards?keyword=Sentinel,Grit&keywordMethod=exclude"]}>
          <FiltersProbe />
        </MemoryRouter>
      );
    });

    expect(latest?.filters.keywords).toEqual(["Sentinel", "Grit"]);
    expect(latest?.filters.keywordMethod).toBe("exclude");
    expect(latest?.search).toContain("keyword=Sentinel%2CGrit");
    expect(latest?.search).toContain("keywordMethod=exclude");

    await act(async () => {
      root.unmount();
    });

    latest = undefined;
  });

  it("restores filter state from URL on reload", async () => {
    const container = document.createElement("div");
    const root = createRoot(container);

    // Simulate a page load with filters in the URL
    await act(async () => {
      root.render(
        <MemoryRouter initialEntries={["/cards?game=Pokemon&q=pikachu&page=3"]}>
          <FiltersProbe />
        </MemoryRouter>
      );
    });

    // Verify filters are correctly restored
    expect(latest?.filters.games).toEqual(["Pokemon"]);
    expect(latest?.filters.q).toBe("pikachu");
    expect(latest?.filters.page).toBe(3);
    expect(latest?.filters.pageSize).toBe(20); // default
    expect(latest?.filters.sets).toEqual([]);
    expect(latest?.filters.rarities).toEqual([]);
    expect(latest?.filters.types).toEqual([]);
    expect(latest?.filters.arenas).toEqual([]);
    expect(latest?.filters.aspects).toEqual([]);
    expect(latest?.filters.aspectMethod).toBe("or");
    expect(latest?.filters.traits).toEqual([]);

    await act(async () => {
      root.unmount();
    });

    latest = undefined;
  });

  it("handles missing URL params with defaults", async () => {
    const container = document.createElement("div");
    const root = createRoot(container);

    // No query params
    await act(async () => {
      root.render(
        <MemoryRouter initialEntries={["/cards"]}>
          <FiltersProbe />
        </MemoryRouter>
      );
    });

    // Verify default values
    expect(latest?.filters.q).toBe("");
    expect(latest?.filters.games).toEqual([]);
    expect(latest?.filters.sets).toEqual([]);
    expect(latest?.filters.rarities).toEqual([]);
    expect(latest?.filters.types).toEqual([]);
    expect(latest?.filters.arenas).toEqual([]);
    expect(latest?.filters.aspects).toEqual([]);
    expect(latest?.filters.aspectMethod).toBe("or");
    expect(latest?.filters.traits).toEqual([]);
    expect(latest?.filters.page).toBe(1);
    expect(latest?.filters.pageSize).toBe(20);
    expect(latest?.filters.sort).toBeUndefined();

    await act(async () => {
      root.unmount();
    });

    latest = undefined;
  });
});
