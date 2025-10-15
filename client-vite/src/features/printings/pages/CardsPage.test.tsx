import { act } from "react-dom/test-utils";
import { createRoot } from "react-dom/client";
import { beforeEach, describe, expect, it, vi, afterEach } from "vitest";
import { createMemoryRouter, RouterProvider } from "react-router-dom";

const usePrintingsMock = vi.fn();
const CardModalMock = vi.fn(() => null);

vi.mock("../api/usePrintings", () => ({
  usePrintings: (query: unknown) => usePrintingsMock(query),
}));

vi.mock("@/features/cards/components/CardModal", () => ({
  default: (props: unknown) => CardModalMock(props),
}));

import CardsPage from "./CardsPage";

describe("CardsPage", () => {
  beforeEach(() => {
    vi.useFakeTimers();
    usePrintingsMock.mockReset();
    CardModalMock.mockReset();
    usePrintingsMock.mockImplementation(() => ({
      data: [],
      isLoading: false,
      isError: false,
      error: null,
    }));
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it("calls usePrintings with the default query", async () => {
    const router = createMemoryRouter([{ path: "/", element: <CardsPage /> }]);

    const container = document.createElement("div");
    const root = createRoot(container);

    await act(async () => {
      root.render(<RouterProvider router={router} />);
    });

    expect(usePrintingsMock).toHaveBeenCalled();
    expect(usePrintingsMock.mock.calls[0]?.[0]).toEqual({
      q: "",
      game: [],
      set: [],
      rarity: [],
      page: 1,
      pageSize: 60,
    });

    await act(async () => {
      root.unmount();
    });
  });

  it("preserves search input value while typing", async () => {
    const router = createMemoryRouter([{ path: "/", element: <CardsPage /> }]);

    usePrintingsMock.mockImplementation(() => ({
      data: [],
      isLoading: false,
      isError: false,
      error: null,
    }));

    const container = document.createElement("div");
    const root = createRoot(container);

    await act(async () => {
      root.render(<RouterProvider router={router} />);
    });

    const input = container.querySelector<HTMLInputElement>("input[type='search']");
    expect(input).not.toBeNull();

    if (input) {
      // Type "Pikachu" and verify input value is preserved
      await act(async () => {
        input.value = "Pikachu";
        input.dispatchEvent(new Event("change", { bubbles: true }));
      });

      // Input value should remain "Pikachu" immediately after typing
      expect(input.value).toBe("Pikachu");

      // Advance time by 100ms (less than debounce delay)
      await act(async () => {
        vi.advanceTimersByTime(100);
      });

      // Input value should still be "Pikachu" during debounce period
      expect(input.value).toBe("Pikachu");

      // Advance time past debounce delay
      await act(async () => {
        vi.advanceTimersByTime(200);
      });

      // Input value should STILL be "Pikachu" after debounce completes
      expect(input.value).toBe("Pikachu");
    }

    await act(async () => {
      root.unmount();
    });
  });

  it("renders printing tiles and updates the query when searching", async () => {
    const router = createMemoryRouter([{ path: "/", element: <CardsPage /> }]);

    usePrintingsMock.mockImplementation(() => ({
      data: [
        {
          printingId: "p1",
          cardId: "c1",
          cardName: "Sample Card",
          game: "Game A",
          setName: "Set A",
          setCode: null,
          number: "001",
          rarity: "Common",
          imageUrl: null,
        },
      ],
      isLoading: false,
      isError: false,
      error: null,
    }));

    const container = document.createElement("div");
    const root = createRoot(container);

    await act(async () => {
      root.render(<RouterProvider router={router} />);
    });

    // Card tiles no longer show visible text - verify aria-label for accessibility
    const cardButton = container.querySelector<HTMLButtonElement>("button[aria-label]");
    expect(cardButton).not.toBeNull();
    expect(cardButton?.getAttribute("aria-label")).toBe("Sample Card — Set A #001");

    const input = container.querySelector<HTMLInputElement>("input[type='search']");
    expect(input).not.toBeNull();

    if (input) {
      await act(async () => {
        input.value = "Pikachu";
        input.dispatchEvent(new Event("change", { bubbles: true }));
      });

      await act(async () => {
        vi.advanceTimersByTime(300);
      });
    }

    const lastCall = usePrintingsMock.mock.calls.at(-1)?.[0];
    expect(lastCall).toMatchObject({ q: "Pikachu" });

    await act(async () => {
      root.unmount();
    });
  });

  it("opens CardModal when a printing is clicked", async () => {
    const router = createMemoryRouter([{ path: "/", element: <CardsPage /> }]);

    usePrintingsMock.mockImplementation(() => ({
      data: [
        {
          printingId: "123",
          cardId: "456",
          cardName: "Test Card",
          game: "Magic",
          setName: "Alpha",
          setCode: "ALP",
          number: "001",
          rarity: "Rare",
          imageUrl: "/test.png",
        },
      ],
      isLoading: false,
      isError: false,
      error: null,
    }));

    const container = document.createElement("div");
    const root = createRoot(container);

    await act(async () => {
      root.render(<RouterProvider router={router} />);
    });

    // Find and click the printing card button
    const printingButton = container.querySelector<HTMLButtonElement>("button[aria-label]");
    expect(printingButton).not.toBeNull();
    expect(printingButton?.getAttribute("aria-label")).toBe("Test Card — Alpha #001");

    await act(async () => {
      printingButton?.click();
    });

    // CardModal should be called with the correct props
    expect(CardModalMock).toHaveBeenCalled();
    const lastCall = CardModalMock.mock.calls.at(-1)?.[0];
    expect(lastCall).toMatchObject({
      cardId: 456,
      initialPrintingId: 123,
      open: true,
    });

    await act(async () => {
      root.unmount();
    });
  });
});
