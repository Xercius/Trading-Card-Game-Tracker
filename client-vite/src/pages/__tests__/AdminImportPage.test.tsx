import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, describe, expect, it, vi } from "vitest";
import { MemoryRouter } from "react-router-dom";
import AdminImportPage from "../AdminImportPage";
import http from "@/lib/http";
import { UPLOAD_MAX_SIZE_MB } from "@/constants";

vi.mock("@/state/useUser", () => ({
  useUser: () => ({
    userId: 1,
    setUserId: () => {},
    users: [],
    refreshUsers: () => Promise.resolve(),
  }),
}));

function createClient() {
  return new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
}

const defaultSyncStatus = {
  source: "swu",
  status: "Idle",
  runningSince: null,
  lastCompletedAt: null,
  historyCount: 0,
  history: [],
  messages: ["No sync history recorded yet."],
};

function renderPage(client: QueryClient) {
  const user = userEvent.setup();
  const view = render(
    <MemoryRouter>
      <QueryClientProvider client={client}>
        <AdminImportPage />
      </QueryClientProvider>
    </MemoryRouter>
  );

  return {
    user,
    cleanupClient: () => client.clear(),
    ...view,
  };
}

describe("AdminImportPage", () => {
  afterEach(() => {
    cleanup();
    vi.restoreAllMocks();
  });

  it("runs a dry-run and renders preview rows", async () => {
    const client = createClient();
    vi.spyOn(http, "get").mockImplementation((url: string) => {
      if (url === "admin/import/options") {
        return Promise.resolve({
          data: {
            sources: [
              {
                key: "dummy",
                importerKey: "dummy",
                displayName: "Dummy Importer",
                games: ["Test Game"],
                sets: [
                  { code: "ALP", name: "Alpha" },
                  { code: "BETA", name: "Beta" },
                ],
              },
            ],
          },
        });
      }
      if (url === "admin/sync/star-wars-unlimited/status") {
        return Promise.resolve({ data: defaultSyncStatus });
      }
      return Promise.reject(new Error(`Unexpected URL ${url}`));
    });

    vi.spyOn(http, "post").mockImplementation((url: string) => {
      if (url === "admin/import/dry-run") {
        return Promise.resolve({
          data: {
            summary: { new: 2, update: 1, duplicate: 0, invalid: 0 },
            rows: [
              {
                externalId: "new",
                name: "New records",
                game: "Test Game",
                set: "ALP",
                rarity: null,
                printingKey: null,
                imageUrl: null,
                price: null,
                status: "New",
                messages: ["2 new"],
              },
            ],
          },
        });
      }
      return Promise.reject(new Error(`Unexpected URL ${url}`));
    });

    const { cleanupClient, user } = renderPage(client);

    await user.click((await screen.findAllByRole("combobox"))[0]);
    await user.click(await screen.findByRole("option", { name: "Dummy Importer" }));

    await user.click(screen.getAllByRole("combobox")[1]);
    await user.click(await screen.findByRole("option", { name: "Alpha (ALP)" }));

    await user.click(screen.getByRole("button", { name: "Dry-run" }));

    expect(await screen.findByText("New: 2")).toBeVisible();
    expect(screen.getByText("Update: 1")).toBeVisible();
    expect(screen.getByText("New records")).toBeVisible();

    cleanupClient();
  });

  it("applies an import and shows a toast", async () => {
    const client = createClient();
    vi.spyOn(http, "get").mockImplementation((url: string) => {
      if (url === "admin/import/options") {
        return Promise.resolve({
          data: {
            sources: [
              {
                key: "dummy",
                importerKey: "dummy",
                displayName: "Dummy Importer",
                games: ["Test Game"],
                sets: [],
              },
            ],
          },
        });
      }
      if (url === "admin/sync/star-wars-unlimited/status") {
        return Promise.resolve({ data: defaultSyncStatus });
      }
      return Promise.reject(new Error(`Unexpected URL ${url}`));
    });

    const postMock = vi.spyOn(http, "post").mockImplementation((url: string) => {
      if (url === "admin/import/dry-run") {
        return Promise.resolve({
          data: {
            summary: { new: 1, update: 0, duplicate: 0, invalid: 0 },
            rows: [],
          },
        });
      }
      if (url === "admin/import/apply") {
        return Promise.resolve({ data: { created: 1, updated: 0, skipped: 0, invalid: 0 } });
      }
      return Promise.reject(new Error(`Unexpected URL ${url}`));
    });

    const { cleanupClient, user } = renderPage(client);

    await user.click((await screen.findAllByRole("combobox"))[0]);
    await user.click(await screen.findByRole("option", { name: "Dummy Importer" }));

    await user.click(screen.getByRole("button", { name: "Dry-run" }));
    await screen.findByText("New: 1");

    await user.click(screen.getByRole("button", { name: "Apply" }));

    await waitFor(() =>
      expect(postMock).toHaveBeenCalledWith(
        "admin/import/apply",
        expect.anything(),
        expect.any(Object)
      )
    );
    expect(await screen.findByText(/Import applied/i)).toBeVisible();
    expect(screen.queryByRole("table")).not.toBeInTheDocument();

    cleanupClient();
  });

  it("validates upload file type and size", async () => {
    const client = createClient();
    vi.spyOn(http, "get").mockImplementation((url: string) => {
      if (url === "admin/import/options") {
        return Promise.resolve({ data: { sources: [] } });
      }
      if (url === "admin/sync/star-wars-unlimited/status") {
        return Promise.resolve({ data: defaultSyncStatus });
      }
      return Promise.reject(new Error(`Unexpected URL ${url}`));
    });

    const { cleanupClient, container, user } = renderPage(client);

    await user.click(await screen.findByRole("button", { name: "Upload file" }));

    const input = container.querySelector("input[type='file']") as HTMLInputElement | null;
    expect(input).not.toBeNull();

    const badFile = new File(["bad"], "bad.txt", { type: "text/plain" });
    fireEvent.change(input!, { target: { files: [badFile] } });
    expect(await screen.findByText(/Unsupported file type/i)).toBeVisible();

    const largeBlob = new Uint8Array(UPLOAD_MAX_SIZE_MB * 1024 * 1024 + 1);
    const largeFile = new File([largeBlob], "huge.csv", { type: "text/csv" });
    fireEvent.change(input!, { target: { files: [largeFile] } });
    expect(await screen.findByText(`File exceeds ${UPLOAD_MAX_SIZE_MB} MB limit.`)).toBeVisible();

    cleanupClient();
  });
});
