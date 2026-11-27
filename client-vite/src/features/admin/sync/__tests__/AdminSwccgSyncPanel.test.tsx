import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { cleanup, render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import http from "@/lib/http";
import AdminSwccgSyncPanel from "../AdminSwccgSyncPanel";
import type { AdminSwccgAlternatePrintingsSyncResponse } from "../api";

const neverSyncedStatus = {
  source: "swccg",
  status: "Idle",
  runningSince: null,
  lastCompletedAt: null as string | null,
  lastOutcome: null as string | null,
  historyCount: 0,
  history: [],
  messages: [],
};

const completedBaseStatus = {
  ...neverSyncedStatus,
  lastCompletedAt: "2026-11-19T12:00:00Z",
  lastOutcome: "Succeeded",
  historyCount: 1,
};

let client: QueryClient;

function renderPanel() {
  client = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
  return render(
    <QueryClientProvider client={client}>
      <AdminSwccgSyncPanel />
    </QueryClientProvider>
  );
}

describe("AdminSwccgSyncPanel", () => {
  beforeEach(() => {
    vi.restoreAllMocks();
  });

  afterEach(() => {
    cleanup();
    client?.clear();
  });

  it("hides Sync Alternate Printings until base SWCCG sync has completed", async () => {
    vi.spyOn(http, "get").mockResolvedValue({ data: neverSyncedStatus });

    renderPanel();

    expect(await screen.findByRole("button", { name: "Run SWCCG sync" })).toBeInTheDocument();
    expect(
      screen.queryByRole("button", { name: "Sync Alternate Printings" })
    ).not.toBeInTheDocument();
  });

  it("shows Sync Alternate Printings after base sync and renders failure report when rows fail", async () => {
    vi.spyOn(http, "get").mockResolvedValue({ data: completedBaseStatus });
    const altResponse: AdminSwccgAlternatePrintingsSyncResponse = {
      source: "swccg-alt-csv",
      status: "CompletedWithWarnings",
      dryRun: false,
      startedAt: "2026-11-20T01:00:00Z",
      completedAt: "2026-11-20T01:00:05Z",
      totalRowsProcessed: 1338,
      newCardsCreated: 17,
      printingsCreated: 1337,
      skippedExisting: 0,
      setsCreated: 5,
      raritiesCreated: 4,
      treatmentsCreated: 2,
      failedCount: 1,
      failures: [
        {
          fileName: "SWCCG_AlternatePrintings.csv",
          rowNumber: 792,
          cardName: "Missing Example Card",
          side: "Light",
          originalSet: "Theed Palace",
          expansion: "First Anthology",
          reason: "No card matching 'Missing Example Card' found in 'Theed Palace'.",
        },
      ],
      messages: ["1 row(s) could not be imported."],
    };

    const postSpy = vi.spyOn(http, "post").mockResolvedValue({ data: altResponse });
    const user = userEvent.setup();
    const writeTextMock = vi.fn().mockResolvedValue(undefined);
    Object.defineProperty(navigator, "clipboard", {
      value: { writeText: writeTextMock },
      configurable: true,
    });

    renderPanel();

    const syncAltButton = await screen.findByRole("button", { name: "Sync Alternate Printings" });
    await user.click(syncAltButton);

    await waitFor(() => {
      expect(postSpy).toHaveBeenCalledWith("admin/sync/star-wars-ccg/alternate-printings", {
        dryRun: false,
      });
    });

    const failureMessage = await screen.findByText(/1 row\(s\) were not imported successfully/i);
    expect(failureMessage.closest('[role="alert"]')).not.toBeNull();
    expect(screen.getByText("SWCCG_AlternatePrintings.csv")).toBeInTheDocument();
    expect(screen.getByText("792")).toBeInTheDocument();
    expect(screen.getByText("Missing Example Card")).toBeInTheDocument();

    await user.click(screen.getByRole("button", { name: "Copy Failure Report" }));
    expect(writeTextMock).toHaveBeenCalledWith(
      expect.stringContaining("[SWCCG_AlternatePrintings.csv:792]")
    );
  });

  it("renders clean completion message with zero failures", async () => {
    vi.spyOn(http, "get").mockResolvedValue({ data: completedBaseStatus });
    const cleanResponse: AdminSwccgAlternatePrintingsSyncResponse = {
      source: "swccg-alt-csv",
      status: "Succeeded",
      dryRun: false,
      startedAt: "2026-11-20T01:00:00Z",
      completedAt: "2026-11-20T01:00:05Z",
      totalRowsProcessed: 1338,
      newCardsCreated: 17,
      printingsCreated: 1338,
      skippedExisting: 0,
      setsCreated: 5,
      raritiesCreated: 4,
      treatmentsCreated: 2,
      failedCount: 0,
      failures: [],
      messages: ["All good."],
    };
    vi.spyOn(http, "post").mockResolvedValue({ data: cleanResponse });

    const user = userEvent.setup();
    renderPanel();

    await user.click(await screen.findByRole("button", { name: "Sync Alternate Printings" }));

    expect(
      await screen.findByText("All alternate printing CSV rows completed without failures.")
    ).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Copy Failure Report" })).not.toBeInTheDocument();
  });
});
