import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { cleanup, render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import http from "@/lib/http";
import { ProblemDetailsError } from "@/lib/problemDetails";
import AdminSyncPanel from "../AdminSyncPanel";

const statusRoute = "admin/sync/star-wars-unlimited/status";
const sessionRoute = "admin/sync/session";
const csrfConfig = { withCredentials: true, headers: { "X-Admin-CSRF": "request-token" } };
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
let authorized: boolean;
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
  authorized = true;
  vi.spyOn(http, "get").mockImplementation(async (url: string) => {
    if (url === sessionRoute)
      return { data: { authenticated: authorized, csrfToken: "request-token" } };
    if (url === statusRoute && authorized) return { data: currentStatus };
    throw new ProblemDetailsError("Unauthorized", { status: 401 });
  });
});

afterEach(() => {
  cleanup();
  client?.clear();
  vi.restoreAllMocks();
});

describe("AdminSyncPanel", () => {
  it("shows global checkpoint and last outcome instead of per-set history", async () => {
    renderPanel();
    expect(await screen.findByRole("heading", { name: "Star Wars Unlimited sync" })).toBeVisible();
    expect(screen.getByText("Last successful global sync")).toBeVisible();
    expect(screen.getByText("Last outcome")).toBeVisible();
    expect(screen.getByText("Succeeded")).toBeVisible();
    expect(screen.queryByText("Latest set")).not.toBeInTheDocument();
    expect(screen.queryByText("Tracked sets")).not.toBeInTheDocument();
    expect(screen.queryByText(/SHD/)).not.toBeInTheDocument();
    expect(http.get).toHaveBeenCalledWith(statusRoute, { withCredentials: true });
  });

  it("signs in with credentials and reloads authorized status", async () => {
    authorized = false;
    const post = vi.spyOn(http, "post").mockImplementation(async () => {
      authorized = true;
      return { data: null };
    });
    renderPanel();
    await userEvent.type(await screen.findByRole("textbox", { name: "Username" }), "admin");
    await userEvent.type(screen.getByLabelText("Password"), "password");
    await userEvent.click(screen.getByRole("button", { name: "Sign in" }));
    expect(await screen.findByRole("button", { name: "Run sync" })).toBeEnabled();
    expect(post).toHaveBeenCalledWith(
      sessionRoute,
      { username: "admin", password: "password" },
      csrfConfig
    );
    expect(screen.queryByLabelText(/token/i)).not.toBeInTheDocument();
  });

  it("shows safe sign-in feedback and clears the password after failure", async () => {
    authorized = false;
    vi.spyOn(http, "post").mockRejectedValue(new Error("private server detail"));
    renderPanel();
    await userEvent.type(await screen.findByRole("textbox", { name: "Username" }), "admin");
    await userEvent.type(screen.getByLabelText("Password"), "wrong");
    await userEvent.click(screen.getByRole("button", { name: "Sign in" }));
    expect(await screen.findByRole("alert")).toHaveTextContent("Sign-in failed.");
    expect(screen.getByLabelText("Password")).toHaveValue("");
    expect(screen.queryByText("private server detail")).not.toBeInTheDocument();
  });

  it("runs sync with cookie credentials and CSRF protection", async () => {
    const post = vi.spyOn(http, "post").mockResolvedValue({ data: success });
    renderPanel();
    await userEvent.click(await screen.findByRole("button", { name: "Run sync" }));
    expect(
      await screen.findByText("Sync completed — 2 sets, 3 created, 4 updated, 0 invalid.")
    ).toBeVisible();
    expect(post).toHaveBeenCalledWith("admin/sync/star-wars-unlimited", null, csrfConfig);
  });

  it("requests full reconciliation explicitly with CSRF protection", async () => {
    const post = vi.spyOn(http, "post").mockResolvedValue({ data: success });
    renderPanel();
    await userEvent.click(await screen.findByRole("button", { name: "Full reconciliation" }));
    await waitFor(() =>
      expect(post).toHaveBeenCalledWith(
        "admin/sync/star-wars-unlimited?forceFullSync=true",
        null,
        csrfConfig
      )
    );
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

  it("shows login again when a mutation session expires", async () => {
    vi.spyOn(http, "post").mockRejectedValue(new ProblemDetailsError("expired", { status: 401 }));
    renderPanel();
    await userEvent.click(await screen.findByRole("button", { name: "Run sync" }));
    expect(await screen.findByRole("button", { name: "Sign in" })).toBeEnabled();
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

  it("signs out with CSRF protection and removes cached status", async () => {
    const remove = vi.spyOn(http, "delete").mockImplementation(async () => {
      authorized = false;
      return { data: null };
    });
    renderPanel();
    await userEvent.click(await screen.findByRole("button", { name: "Sign out" }));
    expect(await screen.findByRole("button", { name: "Sign in" })).toBeVisible();
    expect(remove).toHaveBeenCalledWith(sessionRoute, csrfConfig);
  });
});
