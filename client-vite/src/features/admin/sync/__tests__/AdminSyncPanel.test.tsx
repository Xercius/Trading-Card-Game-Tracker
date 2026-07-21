import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, describe, expect, it, vi } from "vitest";
import http from "@/lib/http";
import AdminSyncPanel from "../AdminSyncPanel";

function createClient() {
  return new QueryClient({
    defaultOptions: {
      queries: { retry: false },
      mutations: { retry: false },
    },
  });
}

function renderPanel() {
  const client = createClient();
  const view = render(
    <QueryClientProvider client={client}>
      <AdminSyncPanel />
    </QueryClientProvider>
  );

  return {
    ...view,
    cleanupClient: () => client.clear(),
  };
}

afterEach(() => {
  vi.restoreAllMocks();
});

describe("AdminSyncPanel", () => {
  it("shows the latest sync details", async () => {
    vi.spyOn(http, "get").mockImplementation((url: string) => {
      if (url === "admin/sync/star-wars-unlimited/status") {
        return Promise.resolve({
          data: {
            source: "swu",
            status: "Idle",
            runningSince: null,
            lastCompletedAt: "2026-07-20T14:15:16Z",
            historyCount: 2,
            history: [
              { setCode: "SHD", lastSyncedAt: "2026-07-20T14:15:16Z" },
              { setCode: "SOR", lastSyncedAt: "2026-07-18T09:00:00Z" },
            ],
            messages: [],
          },
        });
      }

      return Promise.reject(new Error(`Unexpected URL ${url}`));
    });

    const { cleanupClient, unmount } = renderPanel();

    expect(await screen.findByRole("heading", { name: "Star Wars Unlimited sync" })).toBeVisible();
    expect(screen.getByText("Idle")).toBeVisible();
    expect(screen.getByText(/SHD ·/)).toBeVisible();
    expect(screen.getByText("2")).toBeVisible();
    expect(screen.getByRole("button", { name: "Run sync" })).toBeEnabled();

    unmount();
    cleanupClient();
  });

  it("runs a sync and shows the result summary", async () => {
    const getMock = vi.spyOn(http, "get").mockImplementation((url: string) => {
      if (url === "admin/sync/star-wars-unlimited/status") {
        return Promise.resolve({
          data: {
            source: "swu",
            status: "Idle",
            runningSince: null,
            lastCompletedAt: "2026-07-20T14:15:16Z",
            historyCount: 1,
            history: [{ setCode: "SHD", lastSyncedAt: "2026-07-20T14:15:16Z" }],
            messages: [],
          },
        });
      }

      return Promise.reject(new Error(`Unexpected URL ${url}`));
    });
    const postMock = vi.spyOn(http, "post").mockImplementation((url: string) => {
      if (url === "admin/sync/star-wars-unlimited") {
        return Promise.resolve({
          data: {
            source: "swu",
            status: "Succeeded",
            startedAt: "2026-07-21T10:00:00Z",
            completedAt: "2026-07-21T10:05:00Z",
            setCount: 2,
            created: 3,
            updated: 4,
            invalid: 1,
            messages: ["Sync complete"],
          },
        });
      }

      return Promise.reject(new Error(`Unexpected URL ${url}`));
    });

    const user = userEvent.setup();
    const { cleanupClient, unmount } = renderPanel();

    await user.click(await screen.findByRole("button", { name: "Run sync" }));

    await waitFor(() =>
      expect(screen.getByText("Sync completed — 2 sets, 3 created, 4 updated, 1 invalid.")).toBeVisible()
    );
    expect(postMock).toHaveBeenCalledWith("admin/sync/star-wars-unlimited", null);
    await waitFor(() => expect(getMock).toHaveBeenCalledTimes(2));

    unmount();
    cleanupClient();
  });
});
