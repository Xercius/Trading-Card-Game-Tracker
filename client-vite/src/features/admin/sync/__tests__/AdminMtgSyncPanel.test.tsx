import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { cleanup, render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import http from "@/lib/http";
import AdminMtgSyncPanel from "../AdminMtgSyncPanel";
import type { AdminMtgSyncStatusResponse } from "../api";

const sampleStatus: AdminMtgSyncStatusResponse = {
  isRunning: false,
  runningSince: null,
  lastSetsSyncAt: "2026-11-10T12:00:00Z",
  lastCardsSyncAt: "2026-11-10T12:30:00Z",
  lastOutcome: "Succeeded",
  sets: [
    {
      id: 1,
      code: "FDN",
      name: "Foundations",
      era: "Core & Expansions",
      releaseDate: "2024-11-15",
      cardCountInDb: 280,
      isSynced: true,
      lastSyncedAt: "2026-11-10T12:30:00Z",
    },
    {
      id: 2,
      code: "BLB",
      name: "Bloomburrow",
      era: "Core & Expansions",
      releaseDate: "2024-08-02",
      cardCountInDb: 0,
      isSynced: false,
      lastSyncedAt: null,
    },
  ],
};

let client: QueryClient;

function renderPanel() {
  client = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
  return render(
    <QueryClientProvider client={client}>
      <AdminMtgSyncPanel />
    </QueryClientProvider>
  );
}

describe("AdminMtgSyncPanel", () => {
  beforeEach(() => {
    vi.restoreAllMocks();
  });

  afterEach(() => {
    cleanup();
    client?.clear();
  });

  it("renders set coverage and triggers Step 1 Sync Sets", async () => {
    vi.spyOn(http, "get").mockResolvedValue({ data: sampleStatus });
    const postSpy = vi.spyOn(http, "post").mockResolvedValue({
      data: {
        source: "mtg",
        dryRun: false,
        cardsCreated: 0,
        cardsUpdated: 0,
        printingsCreated: 0,
        printingsUpdated: 0,
        errors: 0,
        messages: ["Successfully synced 2 MTG sets."],
      },
    });

    const user = userEvent.setup();
    renderPanel();

    expect(await screen.findByText("1 / 2 sets synced")).toBeInTheDocument();

    await user.click(screen.getByRole("button", { name: "Sync Sets" }));

    await waitFor(() => {
      expect(postSpy).toHaveBeenCalledWith("admin/sync/mtg/sets", null);
    });
    expect(await screen.findByText(/Successfully synced 2 MTG sets\./)).toBeInTheDocument();
  });

  it("locks synced sets as checked and disabled", async () => {
    vi.spyOn(http, "get").mockResolvedValue({ data: sampleStatus });

    const user = userEvent.setup();
    renderPanel();

    await screen.findByText("Core & Expansions");
    expect(screen.getByRole("button", { name: "Sync Cards (0 new, 1 synced)" })).toBeEnabled();

    await user.click(screen.getByRole("button", { name: /Core & Expansions/i }));

    const fdnCheckbox = screen.getByLabelText("Select Foundations");
    expect(fdnCheckbox).toBeChecked();
    expect(fdnCheckbox).toBeDisabled();

    const blbCheckbox = screen.getByLabelText("Select Bloomburrow");
    expect(blbCheckbox).not.toBeChecked();
    expect(blbCheckbox).not.toBeDisabled();
  });

  it("allows selecting an unsynced set and sends both synced and new sets on Sync Cards", async () => {
    vi.spyOn(http, "get").mockResolvedValue({ data: sampleStatus });
    const postSpy = vi.spyOn(http, "post").mockResolvedValue({
      data: {
        source: "mtg",
        dryRun: true,
        cardsCreated: 10,
        cardsUpdated: 0,
        printingsCreated: 20,
        printingsUpdated: 0,
        errors: 0,
        messages: ["Synced 10 cards for set 'BLB'."],
      },
    });

    const user = userEvent.setup();
    renderPanel();

    await screen.findByText("Core & Expansions");
    await user.click(screen.getByRole("button", { name: /Core & Expansions/i }));

    await user.click(screen.getByLabelText("Select Bloomburrow"));
    await user.click(screen.getByLabelText(/Dry run/i));
    await user.click(screen.getByRole("button", { name: "Sync Cards (1 new, 1 synced)" }));

    await waitFor(() => {
      expect(postSpy).toHaveBeenCalledWith("admin/sync/mtg/cards", {
        setCodes: ["FDN", "BLB"],
        forceFullSync: false,
        dryRun: true,
      });
    });
    expect(await screen.findByText(/MTG cards sync completed/)).toBeInTheDocument();
  });

  it("re-syncs already-synced sets when Sync Cards is clicked with 0 new sets selected", async () => {
    vi.spyOn(http, "get").mockResolvedValue({ data: sampleStatus });
    const postSpy = vi.spyOn(http, "post").mockResolvedValue({
      data: {
        source: "mtg",
        dryRun: false,
        cardsCreated: 0,
        cardsUpdated: 2,
        printingsCreated: 0,
        printingsUpdated: 2,
        errors: 0,
        messages: ["Synced 280 cards for set 'FDN'."],
      },
    });

    const user = userEvent.setup();
    renderPanel();

    await screen.findByText("Core & Expansions");
    await user.click(screen.getByRole("button", { name: "Sync Cards (0 new, 1 synced)" }));

    await waitFor(() => {
      expect(postSpy).toHaveBeenCalledWith("admin/sync/mtg/cards", {
        setCodes: ["FDN"],
        forceFullSync: false,
        dryRun: false,
      });
    });
  });
});
