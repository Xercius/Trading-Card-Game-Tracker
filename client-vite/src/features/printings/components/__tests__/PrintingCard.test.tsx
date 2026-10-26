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
    cardTypes: ["Leader", "Leader Unit"],
    aspects: [{ name: "Heroism", count: 2 }],
    releaseDate: "2025-10-03",
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

  it("displays normalized card details and aspect counts", async () => {
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

    const textContent = container.textContent || "";
    expect(textContent).toContain("Sample Card");
    expect(textContent).toContain("Test Set");
    expect(textContent).toContain("001");
    expect(textContent).toContain("Rare");
    expect(textContent).toContain("Leader, Leader Unit");
    expect(textContent).toContain("Heroism ×2");
    expect(textContent).toContain("2025-10-03");

    await act(async () => {
      root.unmount();
    });
    container.remove();
  });

  it("lets landscape images fill the card width without a fixed height", async () => {
    const container = document.createElement("div");
    document.body.appendChild(container);
    const root = createRoot(container);

    await act(async () => {
      root.render(<PrintingCard p={mockPrinting} />);
    });

    const img = container.querySelector("img");
    expect(img?.classList.contains("w-full")).toBe(true);
    expect(img?.classList.contains("h-auto")).toBe(true);
    expect(img?.parentElement?.classList.contains("aspect-[63/88]")).toBe(false);

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
