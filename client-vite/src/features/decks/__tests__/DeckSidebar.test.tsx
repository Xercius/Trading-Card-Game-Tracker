import { act } from "react-dom/test-utils";
import { createRoot } from "react-dom/client";
import { describe, expect, it } from "vitest";
import { useState, type ReactElement } from "react";
import StickyDeckSidebar from "../StickyDeckSidebar";
import type { DeckCardWithAvailability } from "../api";
import {
  CARD_IMAGE_DATA,
  CARD_NAME_DATA,
  DRAG_SOURCE_DATA,
  DRAG_SOURCE_GRID,
  PRINTING_ID_DATA,
} from "../constants";

function render(ui: ReactElement) {
  const container = document.createElement("div");
  const root = createRoot(container);
  act(() => {
    root.render(ui);
  });
  return {
    container,
    cleanup: () => act(() => root.unmount()),
  };
}

type DataTransferStub = {
  data: Record<string, string>;
  effectAllowed: string;
  dropEffect: string;
  setData: (type: string, value: string) => void;
  getData: (type: string) => string;
};

function createDataTransfer(): DataTransferStub {
  const store: Record<string, string> = {};
  return {
    data: store,
    effectAllowed: "all",
    dropEffect: "none",
    setData(type, value) {
      store[type] = value;
    },
    getData(type) {
      return store[type] ?? "";
    },
  } satisfies DataTransferStub;
}

function dispatchDragEvent(target: Element | null, type: string, dataTransfer: DataTransferStub) {
  if (!target) throw new Error(`Missing target for ${type}`);
  const event = new Event(type, { bubbles: true, cancelable: true });
  Object.defineProperty(event, "dataTransfer", {
    configurable: true,
    enumerable: true,
    get: () => dataTransfer,
  });
  target.dispatchEvent(event);
}

describe("StickyDeckSidebar", () => {
  it("applies sticky positioning", () => {
    const row: DeckCardWithAvailability = {
      printingId: 1001,
      cardName: "Test Card",
      imageUrl: null,
      quantityInDeck: 1,
      availability: 2,
      availabilityWithProxies: 2,
    };

    const { container, cleanup } = render(
      <StickyDeckSidebar
        deckName="Test Deck"
        rows={[row]}
        includeProxies={false}
        onIncludeProxiesChange={() => {}}
        onAdjustQuantity={() => {}}
      />
    );

    const sidebar = container.querySelector("[data-testid='deck-sidebar']");
    expect(sidebar).not.toBeNull();
    expect(sidebar?.className).toContain("sticky");

    cleanup();
  });

  it("increments quantity when a grid card is dropped", () => {
    function Harness() {
      const [rows, setRows] = useState<DeckCardWithAvailability[]>([
        {
          printingId: 1001,
          cardName: "Test Card",
          imageUrl: null,
          quantityInDeck: 1,
          availability: 2,
          availabilityWithProxies: 2,
        },
      ]);
      const [includeProxies, setIncludeProxies] = useState(false);

      return (
        <div>
          <button
            type="button"
            data-testid="grid-card"
            draggable
            onDragStart={(event) => {
              event.dataTransfer.setData(PRINTING_ID_DATA, "1001");
              event.dataTransfer.setData(CARD_NAME_DATA, "Test Card");
              event.dataTransfer.setData(CARD_IMAGE_DATA, "");
              event.dataTransfer.setData(DRAG_SOURCE_DATA, DRAG_SOURCE_GRID);
            }}
          >
            Grid Card
          </button>

          <StickyDeckSidebar
            deckName="Deck"
            rows={rows}
            includeProxies={includeProxies}
            onIncludeProxiesChange={setIncludeProxies}
            onAdjustQuantity={(printingId, delta) => {
              setRows((prev) =>
                prev.map((row) => {
                  if (row.printingId !== printingId) return row;
                  const nextQty = Math.max(0, row.quantityInDeck + delta);
                  return {
                    ...row,
                    quantityInDeck: nextQty,
                    availability: Math.max(0, row.availability - delta),
                    availabilityWithProxies: Math.max(0, row.availabilityWithProxies - delta),
                  } satisfies DeckCardWithAvailability;
                })
              );
            }}
          />
        </div>
      );
    }

    const { container, cleanup } = render(<Harness />);

    const gridCard = container.querySelector("[data-testid='grid-card']");
    const dropzone = container.querySelector("[data-testid='deck-sidebar-dropzone']");
    const qty = container.querySelector("[data-testid='deck-row-1001-qty']");
    expect(qty?.textContent).toBe("1");

    const dataTransfer = createDataTransfer();

    act(() => {
      dispatchDragEvent(gridCard, "dragstart", dataTransfer);
      dispatchDragEvent(dropzone, "dragover", dataTransfer);
      dispatchDragEvent(dropzone, "drop", dataTransfer);
    });

    const updatedQty = container.querySelector("[data-testid='deck-row-1001-qty']");
    expect(updatedQty?.textContent).toBe("2");

    cleanup();
  });

  it("decrements quantity when minus button is clicked", () => {
    const rows: DeckCardWithAvailability[] = [
      {
        printingId: 1001,
        cardName: "Test Card",
        imageUrl: null,
        quantityInDeck: 2,
        availability: 1,
        availabilityWithProxies: 1,
      },
    ];

    function Harness() {
      const [currentRows, setRows] = useState(rows);
      return (
        <StickyDeckSidebar
          deckName="Deck"
          rows={currentRows}
          includeProxies={false}
          onIncludeProxiesChange={() => {}}
          onAdjustQuantity={(printingId, delta) => {
            setRows((prev) =>
              prev.map((row) =>
                row.printingId === printingId
                  ? {
                      ...row,
                      quantityInDeck: Math.max(0, row.quantityInDeck + delta),
                      availability: Math.max(0, row.availability - delta),
                      availabilityWithProxies: Math.max(0, row.availabilityWithProxies - delta),
                    }
                  : row
              )
            );
          }}
        />
      );
    }

    const { container, cleanup } = render(<Harness />);

    const button = container.querySelector<HTMLButtonElement>(
      "[data-testid='deck-row-1001-decrement']"
    );
    const qty = container.querySelector("[data-testid='deck-row-1001-qty']");
    expect(qty?.textContent).toBe("2");

    act(() => {
      button?.click();
    });

    const updatedQty = container.querySelector("[data-testid='deck-row-1001-qty']");
    expect(updatedQty?.textContent).toBe("1");

    cleanup();
  });

  it("toggles availability badges when include proxies changes", () => {
    const baseRow: DeckCardWithAvailability = {
      printingId: 42,
      cardName: "Proxy Test",
      imageUrl: null,
      quantityInDeck: 1,
      availability: 0,
      availabilityWithProxies: 2,
    };

    function Harness() {
      const [includeProxies, setIncludeProxies] = useState(false);
      const [rows, setRows] = useState<DeckCardWithAvailability[]>([baseRow]);

      return (
        <StickyDeckSidebar
          deckName="Deck"
          rows={rows}
          includeProxies={includeProxies}
          onIncludeProxiesChange={(next) => {
            setIncludeProxies(next);
            setRows([
              {
                ...baseRow,
                availabilityWithProxies: next ? 2 : 0,
              },
            ]);
          }}
          onAdjustQuantity={() => {}}
        />
      );
    }

    const { container, cleanup } = render(<Harness />);

    const badge = () => container.querySelector("[data-testid='deck-row-42-badge-ap']");
    expect(badge()?.textContent).toContain("0");

    const toggle = container.querySelector<HTMLButtonElement>("[data-testid='toggle-proxies']");
    act(() => {
      toggle?.click();
    });

    expect(badge()?.textContent).toContain("2");

    cleanup();
  });
});
