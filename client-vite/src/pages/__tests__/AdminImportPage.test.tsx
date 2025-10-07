import { afterEach, describe, expect, it, vi } from "vitest";
import { act } from "react-dom/test-utils";
import { createRoot } from "react-dom/client";
import { MemoryRouter } from "react-router-dom";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
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
  return new QueryClient({ defaultOptions: { queries: { retry: false }, mutations: { retry: false } } });
}

async function flush() {
  await act(async () => {
    await Promise.resolve();
  });
}

async function renderPage(client: QueryClient) {
  const container = document.createElement("div");
  document.body.appendChild(container);
  const root = createRoot(container);

  await act(async () => {
    root.render(
      <MemoryRouter>
        <QueryClientProvider client={client}>
          <AdminImportPage />
        </QueryClientProvider>
      </MemoryRouter>,
    );
  });

  await flush();

  return {
    container,
    cleanup: async () => {
      await act(async () => {
        root.unmount();
      });
      container.remove();
      client.clear();
    },
  };
}

describe("AdminImportPage", () => {
  afterEach(() => {
    vi.restoreAllMocks();
  });

  it("runs a dry-run and renders preview rows", async () => {
    const client = createClient();
    vi.spyOn(http, "get").mockResolvedValue({
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

    const { container, cleanup } = await renderPage(client);

    const combobox = container.querySelector('button[role="combobox"]');
    expect(combobox).not.toBeNull();
    combobox && combobox.dispatchEvent(new MouseEvent("click", { bubbles: true }));
    await flush();

    const option = Array.from(container.querySelectorAll('button[role="option"]')).find((el) =>
      el.textContent?.includes("Dummy Importer"),
    );
    expect(option).toBeDefined();
    option && option.dispatchEvent(new MouseEvent("click", { bubbles: true }));
    await flush();

    const setSelect = container.querySelectorAll('button[role="combobox"]')[1];
    expect(setSelect).toBeDefined();
    setSelect && setSelect.dispatchEvent(new MouseEvent("click", { bubbles: true }));
    await flush();

    const setOption = Array.from(container.querySelectorAll('button[role="option"]')).find((el) =>
      el.textContent?.includes("Alpha"),
    );
    expect(setOption).toBeDefined();
    setOption && setOption.dispatchEvent(new MouseEvent("click", { bubbles: true }));
    await flush();

    const dryRunButton = Array.from(container.querySelectorAll("button")).find((button) =>
      button.textContent?.includes("Dry-run"),
    );
    expect(dryRunButton).toBeDefined();
    await act(async () => {
      dryRunButton?.dispatchEvent(new MouseEvent("click", { bubbles: true }));
    });
    await flush();
    await flush();

    expect(container.textContent).toContain("New: 2");
    expect(container.textContent).toContain("Update: 1");
    const tableStatus = container.querySelector("table tbody tr td");
    expect(tableStatus?.textContent).toContain("New");

    await cleanup();
  });

  it("applies an import and shows a toast", async () => {
    const client = createClient();
    vi.spyOn(http, "get").mockResolvedValue({
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

    const { container, cleanup } = await renderPage(client);

    const combobox = container.querySelector('button[role="combobox"]');
    combobox && combobox.dispatchEvent(new MouseEvent("click", { bubbles: true }));
    await flush();
    const option = Array.from(container.querySelectorAll('button[role="option"]')).find((el) =>
      el.textContent?.includes("Dummy Importer"),
    );
    option && option.dispatchEvent(new MouseEvent("click", { bubbles: true }));
    await flush();

    const dryRunButton = Array.from(container.querySelectorAll("button")).find((button) =>
      button.textContent?.includes("Dry-run"),
    );
    await act(async () => {
      dryRunButton?.dispatchEvent(new MouseEvent("click", { bubbles: true }));
    });
    await flush();
    await flush();

    const applyButton = Array.from(container.querySelectorAll("button")).find((button) =>
      button.textContent?.includes("Apply"),
    );
    await act(async () => {
      applyButton?.dispatchEvent(new MouseEvent("click", { bubbles: true }));
    });
    await flush();

    expect(postMock).toHaveBeenCalledWith("admin/import/apply", expect.anything(), expect.any(Object));
    expect(container.textContent).toContain("Import applied");
    const table = container.querySelector("table");
    expect(table).toBeNull();

    await cleanup();
  });

  it("validates upload file type and size", async () => {
    const client = createClient();
    vi.spyOn(http, "get").mockResolvedValue({ data: { sources: [] } });

    const { container, cleanup } = await renderPage(client);

    const uploadTab = Array.from(container.querySelectorAll("button")).find((button) =>
      button.textContent?.includes("Upload file"),
    );
    await act(async () => {
      uploadTab?.dispatchEvent(new MouseEvent("click", { bubbles: true }));
    });
    await flush();

    const input = container.querySelector("input[type='file']") as HTMLInputElement;
    expect(input).not.toBeNull();

    const badFile = new File(["bad"], "bad.txt", { type: "text/plain" });
    await act(async () => {
      const dataTransfer = new DataTransfer();
      dataTransfer.items.add(badFile);
      input.files = dataTransfer.files;
      input.dispatchEvent(new Event("change", { bubbles: true }));
    });
    await flush();
    expect(container.textContent).toContain("Unsupported file type");

    const largeBlob = new Uint8Array(UPLOAD_MAX_SIZE_MB * 1024 * 1024 + 1);
    const largeFile = new File([largeBlob], "huge.csv", { type: "text/csv" });
    await act(async () => {
      const dataTransfer = new DataTransfer();
      dataTransfer.items.add(largeFile);
      input.files = dataTransfer.files;
      input.dispatchEvent(new Event("change", { bubbles: true }));
    });
    await flush();
    expect(container.textContent).toContain(`File exceeds ${UPLOAD_MAX_SIZE_MB} MB limit.`);

    await cleanup();
  });
});
