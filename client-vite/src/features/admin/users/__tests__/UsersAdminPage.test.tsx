import { act } from "react-dom/test-utils";
import { createRoot } from "react-dom/client";
import { describe, expect, it, vi, afterEach } from "vitest";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import UsersAdminPage from "../UsersAdminPage";
import { UserContext, type Ctx } from "@/state/UserContext";
import type { AdminUserApi } from "@/types/user";
import http from "@/lib/http";

const baseUsers: AdminUserApi[] = [
  {
    id: 1,
    name: "Admin",
    username: "admin",
    displayName: "Admin",
    isAdmin: true,
    createdUtc: "2024-01-01T00:00:00Z",
  },
];

type RenderResult = {
  container: HTMLDivElement;
  cleanup: () => Promise<void>;
  refreshUsers: ReturnType<typeof vi.fn<[], Promise<void>>>;
};

async function renderWithProviders(_initialUsers: AdminUserApi[]): Promise<RenderResult> {
  const client = new QueryClient();
  const container = document.createElement("div");
  document.body.appendChild(container);
  const root = createRoot(container);

  const refreshUsers = vi.fn<[], Promise<void>>(async () => {});
  const ctx: Ctx = {
    userId: 1,
    setUserId: () => {},
    users: baseUsers.map((u) => ({ id: u.id, name: u.name ?? u.username ?? "User", isAdmin: Boolean(u.isAdmin) })),
    refreshUsers,
  };

  await act(async () => {
    root.render(
      <QueryClientProvider client={client}>
        <UserContext.Provider value={ctx}>
          <UsersAdminPage />
        </UserContext.Provider>
      </QueryClientProvider>
    );
  });

  await act(async () => {
    await Promise.resolve();
  });

  return {
    container,
    refreshUsers,
    cleanup: async () => {
      await act(async () => {
        root.unmount();
      });
      container.remove();
      client.clear();
    },
  };
}

afterEach(() => {
  vi.restoreAllMocks();
  if ("confirm" in globalThis) {
    // @ts-expect-error allow cleanup in tests
    delete globalThis.confirm;
  }
});

describe("UsersAdminPage", () => {
  it("toggles admin optimistically and rolls back on error", async () => {
    const users: AdminUserApi[] = [
      ...baseUsers,
      {
        id: 2,
        name: "Alice",
        username: "alice",
        displayName: "Alice",
        isAdmin: false,
        createdUtc: "2024-01-01T00:00:00Z",
      },
    ];

    vi.spyOn(http, "get").mockResolvedValue({ data: users });

    let rejectToggle: ((reason?: unknown) => void) | undefined;
    vi.spyOn(http, "put").mockImplementation(
      () =>
        new Promise((_, reject) => {
          rejectToggle = reject;
        }) as Promise<{ data: AdminUserApi }>
    );

    const { container, cleanup, refreshUsers } = await renderWithProviders(users);

    await act(async () => {
      await Promise.resolve();
    });

    const toggles = container.querySelectorAll<HTMLInputElement>("input[type='checkbox']");
    expect(toggles).toHaveLength(2);
    const aliceToggle = toggles[1];
    expect(aliceToggle.checked).toBe(false);

    await act(async () => {
      aliceToggle.checked = true;
      aliceToggle.dispatchEvent(new Event("change", { bubbles: true }));
    });

    expect(aliceToggle.checked).toBe(true);

    await act(async () => {
      rejectToggle?.(new Error("toggle failed"));
      await Promise.resolve();
    });

    expect(aliceToggle.checked).toBe(false);
    expect(container.textContent ?? "").toContain("toggle failed");
    expect(refreshUsers).not.toHaveBeenCalled();

    await cleanup();
  });

  it("disables delete when only one admin remains", async () => {
    const users: AdminUserApi[] = [
      ...baseUsers,
      {
        id: 2,
        name: "Bob",
        username: "bob",
        displayName: "Bob",
        isAdmin: false,
        createdUtc: "2024-01-01T00:00:00Z",
      },
    ];

    vi.spyOn(http, "get").mockResolvedValue({ data: users });

    const { container, cleanup } = await renderWithProviders(users);

    await act(async () => {
      await Promise.resolve();
    });

    const deleteButton = container.querySelector<HTMLButtonElement>("button[data-user-id='1']");
    expect(deleteButton).not.toBeNull();
    expect(deleteButton?.disabled).toBe(true);

    await cleanup();
  });

  it("shows conflict message when delete fails", async () => {
    const users: AdminUserApi[] = [
      ...baseUsers,
      {
        id: 2,
        name: "CoAdmin",
        username: "coadmin",
        displayName: "Co Admin",
        isAdmin: true,
        createdUtc: "2024-01-01T00:00:00Z",
      },
    ];

    vi.spyOn(http, "get").mockResolvedValue({ data: users });
    vi.spyOn(http, "delete").mockRejectedValue({
      isAxiosError: true,
      toJSON: () => ({}),
      message: "Conflict",
      response: {
        status: 409,
        data: { detail: "At least one administrator must remain." },
      },
    });

    const confirmMock = vi.fn(() => true);
    // @ts-expect-error jsdom does not define confirm by default
    globalThis.confirm = confirmMock;

    const { container, cleanup, refreshUsers } = await renderWithProviders(users);

    await act(async () => {
      await Promise.resolve();
    });

    const deleteButton = container.querySelector<HTMLButtonElement>("button[data-user-id='2']");
    expect(deleteButton).not.toBeNull();

    await act(async () => {
      deleteButton!.click();
      await Promise.resolve();
    });

    expect(confirmMock).toHaveBeenCalled();
    expect(container.textContent ?? "").toContain("At least one administrator must remain.");
    expect(refreshUsers.mock).not.toHaveBeenCalled();

    await cleanup();
  });
});
