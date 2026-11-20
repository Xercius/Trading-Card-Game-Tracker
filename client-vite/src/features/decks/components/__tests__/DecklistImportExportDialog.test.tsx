import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";
import http from "@/lib/http";
import { DecklistImportDialog } from "../DecklistImportDialog";
import { DecklistExportDialog } from "../DecklistExportDialog";

describe("DecklistImportDialog & DecklistExportDialog", () => {
  afterEach(() => {
    cleanup();
    vi.restoreAllMocks();
  });

  function renderWithClient(ui: React.ReactElement) {
    const queryClient = new QueryClient({
      defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
    });
    return render(<QueryClientProvider client={queryClient}>{ui}</QueryClientProvider>);
  }

  it("previews resolved/ambiguous/unmatched lines, allows printing selection, and applies import", async () => {
    const postSpy = vi
      .spyOn(http, "post")
      .mockResolvedValueOnce({
        data: {
          resolvedCount: 1,
          ambiguousCount: 1,
          unmatchedCount: 1,
          lines: [
            {
              lineNumber: 1,
              rawLine: "4 Lightning Bolt",
              quantity: 4,
              parsedName: "Lightning Bolt",
              zone: "main",
              status: "ambiguous",
              selectedPrintingId: 1001,
              candidates: [
                {
                  printingId: 1001,
                  cardId: 10,
                  cardName: "Lightning Bolt",
                  setCode: "M10",
                  number: "146",
                  style: "Standard",
                },
                {
                  printingId: 1002,
                  cardId: 10,
                  cardName: "Lightning Bolt",
                  setCode: "STA",
                  number: "42",
                  style: "Foil",
                },
              ],
              message: "2 printings match 'Lightning Bolt'; choose a printing or keep the default.",
            },
            {
              lineNumber: 2,
              rawLine: "2 Abrade (VOW) 139",
              quantity: 2,
              parsedName: "Abrade",
              zone: "sideboard",
              status: "resolved",
              selectedPrintingId: 2001,
              candidates: [],
            },
            {
              lineNumber: 3,
              rawLine: "1 Unknown Card",
              quantity: 1,
              parsedName: "Unknown Card",
              zone: "main",
              status: "unmatched",
              selectedPrintingId: null,
              candidates: [],
              message: "Card 'Unknown Card' was not found in the game catalog.",
            },
          ],
        },
      } as never)
      .mockResolvedValueOnce({ data: { appliedLines: 2, totalCopies: 6 } } as never);

    const onOpenChange = vi.fn();
    renderWithClient(<DecklistImportDialog deckId={5} open={true} onOpenChange={onOpenChange} />);

    fireEvent.change(screen.getByLabelText("Decklist text"), {
      target: { value: "4 Lightning Bolt\nSideboard:\n2 Abrade (VOW) 139\n1 Unknown Card" },
    });
    fireEvent.click(screen.getByRole("button", { name: "Preview" }));

    expect(await screen.findByText("ambiguous")).toBeInTheDocument();
    expect(screen.getByText(/was not found in the game catalog/)).toBeInTheDocument();

    const select = screen.getByLabelText("Select printing for Lightning Bolt");
    fireEvent.change(select, { target: { value: "1002" } });

    fireEvent.click(screen.getByRole("button", { name: "Apply Import (2)" }));

    await waitFor(() => {
      expect(postSpy).toHaveBeenLastCalledWith("decks/5/import-text/apply", {
        items: [
          { cardPrintingId: 1002, quantity: 4, zone: "main" },
          { cardPrintingId: 2001, quantity: 2, zone: "sideboard" },
        ],
        replaceExisting: false,
      });
    });
    expect(onOpenChange).toHaveBeenCalledWith(false);
  });

  it("loads exported decklist text and copies to clipboard", async () => {
    vi.spyOn(http, "get").mockResolvedValue({
      data: {
        deckName: "Red Deck",
        game: "Magic The Gathering",
        format: "Constructed",
        text: "Main Deck:\n4 Lightning Bolt (M10) 146",
        fileName: "Red Deck.txt",
      },
    } as never);

    const writeText = vi.fn().mockResolvedValue(undefined);
    Object.defineProperty(navigator, "clipboard", {
      configurable: true,
      value: { writeText },
    });

    renderWithClient(<DecklistExportDialog deckId={5} open={true} onOpenChange={vi.fn()} />);

    const textarea = await screen.findByLabelText("Exported decklist text");
    await waitFor(() => {
      expect(textarea).toHaveValue("Main Deck:\n4 Lightning Bolt (M10) 146");
    });

    fireEvent.click(screen.getByRole("button", { name: "Copy to Clipboard" }));
    await waitFor(() => {
      expect(writeText).toHaveBeenCalledWith("Main Deck:\n4 Lightning Bolt (M10) 146");
    });
    expect(await screen.findByText("Copied!")).toBeInTheDocument();
  });
});
