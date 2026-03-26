import { act } from "react-dom/test-utils";
import { createRoot } from "react-dom/client";
import { describe, expect, it, vi, afterEach } from "vitest";
import { PrintingTableView } from "../PrintingTableView";
import type { PrintingListItem } from "../../api/printings";

const mockPrintings: PrintingListItem[] = [
  {
    printingId: "p1",
    cardId: "c1",
    cardName: "Alpha Dragon",
    game: "Test Game",
    setName: "Base Set",
    setCode: "BST",
    number: "001",
    rarity: "Rare",
    imageUrl: "https://example.com/card.jpg",
  },
  {
    printingId: "p2",
    cardId: "c2",
    cardName: "Beta Wizard",
    game: "Other Game",
    setName: "Expansion",
    setCode: null,
    number: null,
    rarity: null,
    imageUrl: null,
  },
];

describe("PrintingTableView", () => {
  afterEach(() => {
    document.body.innerHTML = "";
  });

  it("renders a table with all column headers", async () => {
    const container = document.createElement("div");
    document.body.appendChild(container);
    const root = createRoot(container);

    await act(async () => {
      root.render(<PrintingTableView printings={mockPrintings} />);
    });

    const headers = Array.from(container.querySelectorAll("th")).map(
      (th) => th.textContent
    );
    expect(headers).toContain("Name");
    expect(headers).toContain("Game");
    expect(headers).toContain("Set");
    expect(headers).toContain("Set Code");
    expect(headers).toContain("Number");
    expect(headers).toContain("Rarity");

    await act(async () => {
      root.unmount();
    });
    container.remove();
  });

  it("renders a row for each printing with correct data", async () => {
    const container = document.createElement("div");
    document.body.appendChild(container);
    const root = createRoot(container);

    await act(async () => {
      root.render(<PrintingTableView printings={mockPrintings} />);
    });

    const rows = container.querySelectorAll("tbody tr");
    expect(rows).toHaveLength(2);

    const firstRowCells = Array.from(rows[0].querySelectorAll("td")).map(
      (td) => td.textContent
    );
    expect(firstRowCells[0]).toBe("Alpha Dragon");
    expect(firstRowCells[1]).toBe("Test Game");
    expect(firstRowCells[2]).toBe("Base Set");
    expect(firstRowCells[3]).toBe("BST");
    expect(firstRowCells[4]).toBe("001");
    expect(firstRowCells[5]).toBe("Rare");

    await act(async () => {
      root.unmount();
    });
    container.remove();
  });

  it("renders fallback dash for missing optional fields", async () => {
    const container = document.createElement("div");
    document.body.appendChild(container);
    const root = createRoot(container);

    await act(async () => {
      root.render(<PrintingTableView printings={mockPrintings} />);
    });

    const rows = container.querySelectorAll("tbody tr");
    const secondRowCells = Array.from(rows[1].querySelectorAll("td")).map(
      (td) => td.textContent
    );
    expect(secondRowCells[3]).toBe("—"); // setCode
    expect(secondRowCells[4]).toBe("—"); // number
    expect(secondRowCells[5]).toBe("—"); // rarity

    await act(async () => {
      root.unmount();
    });
    container.remove();
  });

  it("calls onRowClick with the clicked printing", async () => {
    const handleClick = vi.fn();
    const container = document.createElement("div");
    document.body.appendChild(container);
    const root = createRoot(container);

    await act(async () => {
      root.render(
        <PrintingTableView printings={mockPrintings} onRowClick={handleClick} />
      );
    });

    const firstRow = container.querySelector<HTMLTableRowElement>("tbody tr");
    await act(async () => {
      firstRow?.click();
    });

    expect(handleClick).toHaveBeenCalledTimes(1);
    expect(handleClick).toHaveBeenCalledWith(mockPrintings[0]);

    await act(async () => {
      root.unmount();
    });
    container.remove();
  });

  it("is keyboard accessible: rows have tabIndex and role when onRowClick is provided", async () => {
    const container = document.createElement("div");
    document.body.appendChild(container);
    const root = createRoot(container);

    await act(async () => {
      root.render(
        <PrintingTableView printings={mockPrintings} onRowClick={vi.fn()} />
      );
    });

    const rows = container.querySelectorAll<HTMLTableRowElement>("tbody tr");
    rows.forEach((row) => {
      expect(row.getAttribute("tabindex")).toBe("0");
      expect(row.getAttribute("role")).toBe("button");
    });

    await act(async () => {
      root.unmount();
    });
    container.remove();
  });

  it("calls onRowClick when Enter key is pressed on a row", async () => {
    const handleClick = vi.fn();
    const container = document.createElement("div");
    document.body.appendChild(container);
    const root = createRoot(container);

    await act(async () => {
      root.render(
        <PrintingTableView printings={mockPrintings} onRowClick={handleClick} />
      );
    });

    const firstRow = container.querySelector<HTMLTableRowElement>("tbody tr");
    await act(async () => {
      firstRow?.dispatchEvent(
        new KeyboardEvent("keydown", { key: "Enter", bubbles: true })
      );
    });

    expect(handleClick).toHaveBeenCalledTimes(1);
    expect(handleClick).toHaveBeenCalledWith(mockPrintings[0]);

    await act(async () => {
      root.unmount();
    });
    container.remove();
  });
});
