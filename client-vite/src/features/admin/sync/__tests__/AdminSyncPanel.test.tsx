import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { cleanup, render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import http from "@/lib/http";
import { ProblemDetailsError } from "@/lib/problemDetails";
import AdminSyncPanel from "../AdminSyncPanel";

const statusRoute = "admin/sync/star-wars-unlimited/status";
const idle = {
  source: "swu",
  status: "Idle",
  runningSince: null,
  lastCompletedAt: "2026-07-20T14:15:16Z",
  lastOutcome: "Succeeded",
  historyCount: 2,
  history: [{ setCode: "SHD", lastSyncedAt: "2026-07-20T14:15:16Z" }],
  messages: [],
};
const success = { status: "Succeeded", invalid: 0, setCount: 2, created: 3, updated: 4 };
let currentStatus: typeof idle;
let client: QueryClient;

function renderPanel() {
  client = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
  return render(
    <QueryClientProvider client={client}>
      <AdminSyncPanel />
    </QueryClientProvider>
  );
}

beforeEach(() => {
  currentStatus = { ...idle };
  vi.spyOn(http, "get").mockImplementation(async (url: string) => {
    if (url === statusRoute) return { data: currentStatus };
    throw new Error(`Unexpected GET ${url}`);
  });
});

afterEach(() => {
  cleanup();
  client?.clear();
  vi.restoreAllMocks();
});

describe("AdminSyncPanel", () => {
  it("runs sync directly without login, bearer tokens, or session credentials", async () => {
    vi.mocked(http.get).mockRestore();
    const originalAdapter = http.defaults.adapter;
    const requests: {
      url?: string;
      authorization: unknown;
      csrf: unknown;
      withCredentials?: boolean;
    }[] = [];
    http.defaults.adapter = async (config) => {
      requests.push({
        url: config.url,
        authorization: config.headers.get("Authorization"),
        csrf: config.headers.get("X-Admin-CSRF"),
        withCredentials: config.withCredentials,
      });
      const data = config.method === "post" ? success : idle;
      return { data, status: 200, statusText: "OK", headers: {}, config };
    };

    try {
      renderPanel();
      await userEvent.click(await screen.findByRole("button", { name: "Run sync" }));
      expect(await screen.findByText(/Sync completed —/)).toBeVisible();
      await userEvent.click(screen.getByRole("button", { name: "Full reconciliation" }));
      await waitFor(() =>
        expect(requests.some((request) => request.url?.endsWith("?forceFullSync=true"))).toBe(true)
      );

      expect(requests.every((request) => !request.authorization)).toBe(true);
      expect(requests.every((request) => !request.csrf)).toBe(true);
      expect(requests.every((request) => !request.withCredentials)).toBe(true);
      expect(screen.queryByRole("button", { name: "Sign in" })).not.toBeInTheDocument();
      expect(screen.queryByRole("button", { name: "Sign out" })).not.toBeInTheDocument();
    } finally {
      http.defaults.adapter = originalAdapter;
    }
  });

  it("shows global checkpoint and last outcome instead of per-set history", async () => {
    renderPanel();
    expect(screen.getByRole("status")).toHaveTextContent("Loading sync status…");
    expect(await screen.findByRole("heading", { name: "Star Wars Unlimited sync" })).toBeVisible();
    expect(screen.getByText("Last successful global sync")).toBeVisible();
    expect(screen.getByText("Last outcome")).toBeVisible();
    expect(screen.getByText("Succeeded")).toBeVisible();
    expect(screen.queryByText("Latest set")).not.toBeInTheDocument();
    expect(screen.queryByText("Tracked sets")).not.toBeInTheDocument();
    expect(screen.queryByText(/SHD/)).not.toBeInTheDocument();
    expect(http.get).toHaveBeenCalledWith(statusRoute);
  });

  it("runs sync directly", async () => {
    const post = vi.spyOn(http, "post").mockResolvedValue({ data: success });
    renderPanel();
    await userEvent.click(await screen.findByRole("button", { name: "Run sync" }));
    expect(
      await screen.findByText("Sync completed — 2 sets, 3 created, 4 updated, 0 invalid.")
    ).toBeVisible();
    expect(post).toHaveBeenCalledWith("admin/sync/star-wars-unlimited", null);
  });

  it("requests full reconciliation explicitly", async () => {
    const post = vi.spyOn(http, "post").mockResolvedValue({ data: success });
    renderPanel();
    await userEvent.click(await screen.findByRole("button", { name: "Full reconciliation" }));
    await waitFor(() =>
      expect(post).toHaveBeenCalledWith("admin/sync/star-wars-unlimited?forceFullSync=true", null)
    );
  });

  it.each([false, true])(
    "shows progress while the requested sync is pending (full=%s)",
    async (full) => {
      let finishSync: (_result: { data: typeof success }) => void = () => {};
      const pending = new Promise<{ data: typeof success }>((resolve) => {
        finishSync = resolve;
      });
      vi.spyOn(http, "post").mockReturnValue(pending);
      renderPanel();
      await userEvent.click(
        await screen.findByRole("button", {
          name: full ? "Full reconciliation" : "Run sync",
        })
      );
      expect(screen.getByRole("status", { name: "Sync progress" })).toHaveTextContent(
        full ? "Full reconciliation is running…" : "Sync is running…"
      );
      expect(screen.getByRole("button", { name: "Running sync…" })).toBeDisabled();
      expect(screen.getByRole("button", { name: "Full reconciliation" })).toBeDisabled();
      expect(screen.getByRole("button", { name: "Refresh" })).toBeDisabled();
      expect(screen.getByRole("status", { name: "Sync outcome" })).toBeEmptyDOMElement();

      finishSync({ data: success });

      expect(
        await screen.findByText("Sync completed — 2 sets, 3 created, 4 updated, 0 invalid.")
      ).toBeVisible();
      expect(screen.getByRole("status", { name: "Sync progress" })).toBeEmptyDOMElement();
      expect(screen.getByRole("button", { name: "Run sync" })).toBeEnabled();
    }
  );

  it("offers a retry after status loading fails without exposing raw errors", async () => {
    vi.mocked(http.get).mockRejectedValueOnce(
      new ProblemDetailsError("private detail", { status: 500 })
    );
    renderPanel();
    expect(await screen.findByText("Failed to load sync status.")).toBeVisible();
    expect(screen.queryByText("private detail")).not.toBeInTheDocument();
    await userEvent.click(screen.getByRole("button", { name: "Retry" }));
    expect(await screen.findByRole("button", { name: "Run sync" })).toBeEnabled();
  });

  it("preserves the global checkpoint in partial failure feedback", async () => {
    vi.spyOn(http, "post").mockResolvedValue({ data: { status: "Failed", invalid: 1 } });
    renderPanel();
    await userEvent.click(await screen.findByRole("button", { name: "Run sync" }));
    expect(await screen.findByText(/last successful global checkpoint is unchanged/)).toBeVisible();
    expect(screen.queryByText(/Sync completed —/)).not.toBeInTheDocument();
  });

  it("does not expose raw sync errors", async () => {
    vi.spyOn(http, "post").mockRejectedValue(new Error("private backend error"));
    renderPanel();
    await userEvent.click(await screen.findByRole("button", { name: "Run sync" }));
    expect(await screen.findByRole("alert", { name: "Sync error" })).toHaveTextContent(
      "Failed to run sync."
    );
    expect(screen.queryByText("private backend error")).not.toBeInTheDocument();
  });

  it("disables mutation controls while a sync is running", async () => {
    currentStatus.status = "Running";
    renderPanel();
    expect(await screen.findByRole("status", { name: "Sync progress" })).toHaveTextContent(
      "Sync is running"
    );
    expect(screen.getByRole("button", { name: "Run sync" })).toBeDisabled();
    expect(screen.getByRole("button", { name: "Full reconciliation" })).toBeDisabled();
  });
});
