import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";
import { BackupRestoreDialog } from "../components/BackupRestoreDialog";
import * as importExportApi from "../api";
import http from "@/lib/http";

describe("BackupRestoreDialog", () => {
  afterEach(() => {
    cleanup();
    vi.restoreAllMocks();
  });

  function renderDialog() {
    const queryClient = new QueryClient({
      defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
    });
    return render(
      <QueryClientProvider client={queryClient}>
        <BackupRestoreDialog open={true} onOpenChange={vi.fn()} />
      </QueryClientProvider>
    );
  }

  it("triggers JSON backup download when clicking Download JSON Backup", async () => {
    const spy = vi
      .spyOn(importExportApi, "downloadJsonBackup")
      .mockResolvedValue("tcgtracker_export.json");

    renderDialog();
    fireEvent.click(screen.getByRole("button", { name: "Download JSON Backup" }));

    expect(await screen.findByText("Downloaded tcgtracker_export.json")).toBeInTheDocument();
    expect(spy).toHaveBeenCalledTimes(1);
  });

  it("requires confirmation for replace mode and renders restore summary with unresolved warnings", async () => {
    const postSpy = vi.spyOn(http, "post").mockResolvedValue({
      data: {
        imported: {
          collection: 2,
          wishlist: 1,
          decks: 1,
          deckCards: 5,
          resolvedByNaturalKey: 1,
          unresolvedCount: 1,
        },
        unresolved: [
          {
            section: "collection",
            cardPrintingId: 99,
            game: "Magic",
            cardName: "Missing Promo",
            setCode: "PRM",
            number: "1",
            reason: "No printing found.",
          },
        ],
      },
    } as never);

    renderDialog();

    const payload = {
      version: 1,
      collection: [{ cardPrintingId: 1, qtyOwned: 2, qtyProxyOwned: 0 }],
      wishlist: [],
      decks: [{ game: "Magic", name: "Burn", cards: [] }],
    };
    const file = new File([JSON.stringify(payload)], "backup.json", { type: "application/json" });
    const fileInput = screen.getByLabelText("Backup JSON file");
    fireEvent.change(fileInput, { target: { files: [file] } });

    expect(await screen.findByText(/Ready to restore/)).toBeInTheDocument();
    const restoreBtn = screen.getByRole("button", { name: "Restore Backup" });
    expect(restoreBtn).toBeEnabled();

    fireEvent.click(screen.getByLabelText(/Replace \(overwrite all current/));
    expect(restoreBtn).toBeDisabled();

    fireEvent.click(screen.getByLabelText(/I understand Replace mode deletes/));
    expect(restoreBtn).toBeEnabled();

    fireEvent.click(restoreBtn);

    await waitFor(() => {
      expect(screen.getByText("Backup restored successfully")).toBeInTheDocument();
    });
    expect(screen.getByText(/Matched 1 printing\(s\)/)).toBeInTheDocument();
    expect(screen.getByText(/Missing Promo/)).toBeInTheDocument();
    expect(postSpy).toHaveBeenCalledWith("import/json", payload, { params: { mode: "replace" } });
  });
});
