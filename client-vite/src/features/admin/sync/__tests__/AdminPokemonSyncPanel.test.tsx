import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { cleanup, render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import http from "@/lib/http";
import AdminPokemonSyncPanel from "../AdminPokemonSyncPanel";
import type { AdminPokemonSyncStatusResponse } from "../api";

const statusRoute = "admin/sync/pokemon/status";

const sampleStatus: AdminPokemonSyncStatusResponse = {
  isRunning: false,
  runningSince: null,
  lastSetsSyncAt: "2026-11-10T12:00:00Z",
  lastCardsSyncAt: "2026-11-10T12:30:00Z",
  lastOutcome: "Succeeded",
  sets: [
    {
      id: 1,
      code: "bw01",
      name: "Black & White",
      era: "Black & White",
      releaseDate: "2011-04-25",
      cardCountInDb: 115,
      isSynced: true,
      lastSyncedAt: "2026-11-10T12:30:00Z",
    },
    {
      id: 2,
      code: "bw02",
      name: "Emerging Powers",
      era: "Black & White",
      releaseDate: "2011-08-31",
      cardCountInDb: 0,
      isSynced: false,
      lastSyncedAt: null,
    },
    {
      id: 3,
      code: "sv01",
      name: "Scarlet & Violet",
      era: "Scarlet & Violet",
      releaseDate: "2023-03-31",
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
      <AdminPokemonSyncPanel />
    </QueryClientProvider>
  );
}

beforeEach(() => {
  vi.spyOn(http, "get").mockImplementation(async (url: string) => {
    if (url === statusRoute) {
      return { data: sampleStatus } as never;
    }
    throw new Error(`Unexpected GET ${url}`);
  });
});

afterEach(() => {
  cleanup();
  client?.clear();
  vi.restoreAllMocks();
});

describe("AdminPokemonSyncPanel", () => {
  it("renders eras and starts with eras collapsed", async () => {
    renderPanel();

    // Wait for data query to resolve and render era headers
    await waitFor(() => {
      expect(screen.getByText("Black & White")).toBeInTheDocument();
    });

    expect(screen.getByText("Scarlet & Violet")).toBeInTheDocument();

    // Sets inside the eras should not be visible initially because eras start collapsed
    expect(screen.queryByText("Emerging Powers")).not.toBeInTheDocument();
  });

  it("expands an era when clicked to reveal sets", async () => {
    const user = userEvent.setup();
    renderPanel();

    await waitFor(() => {
      expect(screen.getByText("Black & White")).toBeInTheDocument();
    });

    // Click Black & White era header
    await user.click(screen.getByText("Black & White"));

    // Emerging Powers should now be visible
    expect(await screen.findByText("Emerging Powers")).toBeInTheDocument();
    expect(screen.getByText("bw02")).toBeInTheDocument();
  });

  it("locks synced sets as checked and disabled", async () => {
    const user = userEvent.setup();
    renderPanel();

    await waitFor(() => {
      expect(screen.getByText("Black & White")).toBeInTheDocument();
    });

    await user.click(screen.getByText("Black & White"));

    // Wait for sets to expand
    expect(await screen.findByText("Emerging Powers")).toBeInTheDocument();

    // The synced set bw01 has a disabled and checked checkbox in its row
    const bw01Row = screen.getByText("bw01").closest("tr")!;
    const disabledCheckbox = within(bw01Row).getByRole("checkbox");
    expect(disabledCheckbox).toBeDisabled();
    expect(disabledCheckbox).toBeChecked();

    // "Synced" badge appears in bw01's row
    expect(within(bw01Row).getByText("Synced")).toBeInTheDocument();
  });

  it("can select unsynced sets and enables Sync Cards button", async () => {
    const user = userEvent.setup();
    renderPanel();

    await waitFor(() => {
      expect(screen.getByText("Black & White")).toBeInTheDocument();
    });

    // Expand Black & White
    await user.click(screen.getByText("Black & White"));

    expect(await screen.findByText("Emerging Powers")).toBeInTheDocument();

    // Initially 0 selected, button disabled
    const syncCardsBtn = screen.getByRole("button", { name: /Sync Cards \(0 selected\)/i });
    expect(syncCardsBtn).toBeDisabled();

    // Find the checkbox for Emerging Powers (bw02) row
    const bw02Row = screen.getByText("Emerging Powers").closest("tr")!;
    const setCheckbox = within(bw02Row).getByRole("checkbox");
    expect(setCheckbox).not.toBeDisabled();
    expect(setCheckbox).not.toBeChecked();

    // Check bw02
    await user.click(setCheckbox);

    // Button should now show 1 selected and be enabled
    await waitFor(() => {
      expect(screen.getByRole("button", { name: /Sync Cards \(1 selected\)/i })).toBeEnabled();
    });
  });

  it("calls Sync Sets endpoint when button clicked", async () => {
    const postSpy = vi.spyOn(http, "post").mockResolvedValue({
      data: { messages: ["Successfully synced 3 sets."] },
    } as never);

    const user = userEvent.setup();
    renderPanel();

    await waitFor(() => {
      expect(screen.getByText("Sync Sets")).toBeInTheDocument();
    });

    await user.click(screen.getByText("Sync Sets"));

    expect(postSpy).toHaveBeenCalledWith("admin/sync/pokemon/sets", null);
  });
});
