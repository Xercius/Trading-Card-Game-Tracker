import { act } from "react-dom/test-utils";
import { createRoot } from "react-dom/client";
import { describe, expect, it, afterEach } from "vitest";
import { PrintingCard } from "../PrintingCard";
import type { PrintingListItem } from "../../api/printings";

describe("PrintingCard", () => {
  const mockPrinting: PrintingListItem = {
    printingId: "p1",
    cardId: "c1",
    cardName: "Sample Card",
    game: "Test Game",
    setName: "Test Set",
    setCode: "TST",
    number: "001",
    rarity: "Rare",
    imageUrl: "https://example.com/card.jpg",
  };

  afterEach(() => {
    document.body.innerHTML = "";
  });

  it("renders with accessible name including card name, set, and number", async () => {
    const container = document.createElement("div");
    document.body.appendChild(container);
    const root = createRoot(container);

    await act(async () => {
      root.render(<PrintingCard p={mockPrinting} />);
    });

    const button = container.querySelector<HTMLButtonElement>("button");
    expect(button).not.toBeNull();
    expect(button?.getAttribute("aria-label")).toBe("Sample Card — Test Set #001");

    await act(async () => {
      root.unmount();
    });
    container.remove();
  });

  it("renders accessible name without number when not provided", async () => {
    const printingWithoutNumber = { ...mockPrinting, number: null };
    const container = document.createElement("div");
    document.body.appendChild(container);
    const root = createRoot(container);

    await act(async () => {
      root.render(<PrintingCard p={printingWithoutNumber} />);
    });

    const button = container.querySelector<HTMLButtonElement>("button");
    expect(button).not.toBeNull();
    expect(button?.getAttribute("aria-label")).toBe("Sample Card — Test Set");

    await act(async () => {
      root.unmount();
    });
    container.remove();
  });

  it("does not display visible text for card details", async () => {
    const container = document.createElement("div");
    document.body.appendChild(container);
    const root = createRoot(container);

    await act(async () => {
      root.render(<PrintingCard p={mockPrinting} />);
    });

    // Image should have empty alt since button has aria-label
    const img = container.querySelector("img");
    expect(img).not.toBeNull();
    expect(img?.getAttribute("alt")).toBe("");

    // No visible text elements should exist (except "No image" placeholder)
    // Card name, set, number, and rarity should not be visible
    const textContent = container.textContent || "";
    expect(textContent).not.toContain("Sample Card");
    expect(textContent).not.toContain("Test Set");
    expect(textContent).not.toContain("001");
    expect(textContent).not.toContain("Rare");

    await act(async () => {
      root.unmount();
    });
    container.remove();
  });

  it("shows 'No image' text when imageUrl is not provided", async () => {
    const printingWithoutImage = { ...mockPrinting, imageUrl: null };
    const container = document.createElement("div");
    document.body.appendChild(container);
    const root = createRoot(container);

    await act(async () => {
      root.render(<PrintingCard p={printingWithoutImage} />);
    });

    expect(container.textContent).toContain("No image");

    await act(async () => {
      root.unmount();
    });
    container.remove();
  });
});
