import { act } from "react-dom/test-utils";
import { createRoot } from "react-dom/client";
import { MemoryRouter, useSearchParams } from "react-router-dom";
import { afterEach, describe, expect, it, vi } from "vitest";
import { useIncludeProxies } from "../useIncludeProxies";
import { LS_INCLUDE_PROXIES_KEY } from "@/constants";
import { collectionKeys } from "@/features/collection/api";
import { deckBuilderKeys } from "@/features/decks/api";

type ProbeState = {
  includeProxies: boolean;
  setIncludeProxies: ReturnType<typeof useIncludeProxies>[1];
  search: string;
};

let latest: ProbeState | undefined;

function IncludeProxiesProbe() {
  const [includeProxies, setIncludeProxies] = useIncludeProxies();
  const [params] = useSearchParams();
  latest = {
    includeProxies,
    setIncludeProxies,
    search: params.toString(),
  } satisfies ProbeState;
  return null;
}

afterEach(() => {
  window.localStorage.clear();
  latest = undefined;
});

async function renderProbe(initialEntry: string) {
  const container = document.createElement("div");
  const root = createRoot(container);

  await act(async () => {
    root.render(
      <MemoryRouter initialEntries={[initialEntry]}>
        <IncludeProxiesProbe />
      </MemoryRouter>
    );
  });

  return {
    cleanup: async () => {
      await act(async () => {
        root.unmount();
      });
      container.remove();
    },
  };
}

describe("useIncludeProxies", () => {
  it("reads the stored value by default and updates storage when toggled", async () => {
    window.localStorage.setItem(LS_INCLUDE_PROXIES_KEY, "1");
    const getSpy = vi.spyOn(window.localStorage, "getItem");
    const setSpy = vi.spyOn(window.localStorage, "setItem");

    const { cleanup } = await renderProbe("/");

    expect(getSpy).toHaveBeenCalledWith(LS_INCLUDE_PROXIES_KEY);
    expect(latest?.includeProxies).toBe(true);
    expect(latest?.search).toBe("");
    expect(setSpy).not.toHaveBeenCalled();

    await act(async () => {
      latest?.setIncludeProxies(false);
    });

    expect(latest?.includeProxies).toBe(false);
    expect(latest?.search).toBe("");
    expect(setSpy).toHaveBeenCalledWith(LS_INCLUDE_PROXIES_KEY, "0");
    expect(window.localStorage.getItem(LS_INCLUDE_PROXIES_KEY)).toBe("0");

    await cleanup();
    getSpy.mockRestore();
    setSpy.mockRestore();
  });

  it("prioritizes the query parameter and keeps storage in sync", async () => {
    window.localStorage.setItem(LS_INCLUDE_PROXIES_KEY, "0");
    const getSpy = vi.spyOn(window.localStorage, "getItem");
    const setSpy = vi.spyOn(window.localStorage, "setItem");

    const { cleanup } = await renderProbe("/?includeProxies=1");

    expect(latest?.includeProxies).toBe(true);
    expect(latest?.search).toBe("includeProxies=1");
    expect(getSpy).not.toHaveBeenCalled();
    expect(setSpy).toHaveBeenNthCalledWith(1, LS_INCLUDE_PROXIES_KEY, "1");

    await act(async () => {
      latest?.setIncludeProxies(false);
    });

    expect(latest?.includeProxies).toBe(false);
    expect(latest?.search).toBe("");
    expect(setSpy).toHaveBeenNthCalledWith(2, LS_INCLUDE_PROXIES_KEY, "0");
    expect(window.localStorage.getItem(LS_INCLUDE_PROXIES_KEY)).toBe("0");
    expect(getSpy).not.toHaveBeenCalled();

    await cleanup();
    getSpy.mockRestore();
    setSpy.mockRestore();
  });

  it("includes the includeProxies discriminator in query keys", () => {
    const commonCollectionParams = {
      userId: 1,
      page: 1,
      pageSize: 25,
      filters: { q: "", game: "", set: "", rarity: "" },
    };
    const collectionKeyWithProxies = collectionKeys.list({
      ...commonCollectionParams,
      includeProxies: true,
    });
    const collectionKeyWithoutProxies = collectionKeys.list({
      ...commonCollectionParams,
      includeProxies: false,
    });

    expect(collectionKeyWithProxies[1]).toMatchObject({ includeProxies: true });
    expect(collectionKeyWithoutProxies[1]).toMatchObject({ includeProxies: false });
    expect(collectionKeyWithProxies[1]).not.toEqual(collectionKeyWithoutProxies[1]);

    const deckKeyWithProxies = deckBuilderKeys.cards(123, true);
    const deckKeyWithoutProxies = deckBuilderKeys.cards(123, false);

    expect(deckKeyWithProxies[3]).toMatchObject({ includeProxies: true });
    expect(deckKeyWithoutProxies[3]).toMatchObject({ includeProxies: false });
    expect(deckKeyWithProxies).not.toEqual(deckKeyWithoutProxies);
  });
});
