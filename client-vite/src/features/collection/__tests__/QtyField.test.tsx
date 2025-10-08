import { act } from "react-dom/test-utils";
import { createRoot } from "react-dom/client";
import { describe, expect, it, vi } from "vitest";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import QtyField from "../QtyField";
import { collectionKeys, type CollectionItem, type Paged } from "../api";
import { api } from "@/lib/api";
import { MAX_QTY } from "@/constants";

const baseItem: CollectionItem = {
  cardPrintingId: 1,
  quantityOwned: 1,
  quantityWanted: 0,
  quantityProxyOwned: 0,
  availability: 1,
  availabilityWithProxies: 1,
  cardId: 1,
  cardName: "Test",
  game: "Game",
  set: "Set",
  number: "001",
  rarity: "Common",
  style: "",
  imageUrl: null,
};

const queryKey = collectionKeys.list({
  userId: 1,
  page: 1,
  pageSize: 50,
  filters: { q: "", game: "", set: "", rarity: "" },
  includeProxies: false,
});

function renderQtyField(item: CollectionItem) {
  const client = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
  const data: Paged<CollectionItem> = { items: [item], total: 1, page: 1, pageSize: 50 };
  client.setQueryData(queryKey, data);

  const container = document.createElement("div");
  document.body.appendChild(container);
  const root = createRoot(container);

  act(() => {
    root.render(
      <QueryClientProvider client={client}>
        <QtyField
          printingId={item.cardPrintingId}
          value={item.quantityOwned}
          otherValue={item.quantityProxyOwned}
          field="owned"
          queryKey={queryKey}
        />
      </QueryClientProvider>
    );
  });

  return {
    client,
    container,
    root,
    cleanup: () => {
      act(() => {
        root.unmount();
      });
      container.remove();
      client.clear();
    },
  };
}

describe("QtyField", () => {
  it("clamps to MAX_QTY and sends clamped value", async () => {
    const putSpy = vi.spyOn(api, "put").mockResolvedValue({ data: null } as never);
    const item = {
      ...baseItem,
      quantityOwned: MAX_QTY - 1,
      availability: MAX_QTY - 1,
      availabilityWithProxies: MAX_QTY - 1,
    };
    const { container, cleanup } = renderQtyField(item);

    const inc = container.querySelector<HTMLButtonElement>('button[aria-label="Increase"]');
    expect(inc).not.toBeNull();

    await act(async () => {
      inc?.click();
      await Promise.resolve();
    });

    const input = container.querySelector<HTMLInputElement>("input");
    expect(input?.value).toBe(String(MAX_QTY));
    expect(putSpy).toHaveBeenCalledWith("collection/1", { ownedQty: MAX_QTY, proxyQty: 0 });

    await cleanup();
    putSpy.mockRestore();
  });

  it("optimistically updates value before the request resolves", async () => {
    let resolveFn: (() => void) | null = null;
    const putSpy = vi.spyOn(api, "put").mockImplementation(
      () =>
        new Promise((resolve) => {
          resolveFn = () => resolve({ data: null } as never);
        })
    );

    const { container, cleanup } = renderQtyField(baseItem);
    const inc = container.querySelector<HTMLButtonElement>('button[aria-label="Increase"]');

    await act(async () => {
      inc?.click();
    });

    const input = container.querySelector<HTMLInputElement>("input");
    expect(input?.value).toBe("2");

    resolveFn?.();
    await act(async () => {
      await Promise.resolve();
    });

    expect(putSpy).toHaveBeenCalledOnce();

    await cleanup();
    putSpy.mockRestore();
  });

  it("rolls back on error", async () => {
    const putSpy = vi.spyOn(api, "put").mockRejectedValue(new Error("fail"));
    const { container, cleanup } = renderQtyField(baseItem);
    const inc = container.querySelector<HTMLButtonElement>('button[aria-label="Increase"]');

    await act(async () => {
      inc?.click();
      await Promise.resolve();
    });

    const input = container.querySelector<HTMLInputElement>("input");
    expect(input?.value).toBe("1");
    const error = container.querySelector("p.text-destructive");
    expect(error?.textContent).toContain("Update failed");

    await cleanup();
    putSpy.mockRestore();
  });
});
