import { describe, it, expect, vi } from "vitest";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { createRoot } from "react-dom/client";
import { act } from "react-dom/test-utils";
import type { ReactElement } from "react";
import UsersPage from "./UsersPage";
import { UserContext, type Ctx } from "@/state/UserContext";
import http from "@/lib/http";

function renderWithProviders(ui: ReactElement, ctx: Ctx) {
  const client = new QueryClient();
  const container = document.createElement("div");
  document.body.appendChild(container);
  const root = createRoot(container);

  act(() => {
    root.render(
      <QueryClientProvider client={client}>
        <UserContext.Provider value={ctx}>{ui}</UserContext.Provider>
      </QueryClientProvider>
    );
  });

  return {
    container,
    async cleanup() {
      await act(async () => {
        root.unmount();
      });
      container.remove();
      client.clear();
    },
  };
}

describe("UsersPage admin guard", () => {
  it("does not call API when user is not admin", async () => {
    const getSpy = vi.spyOn(http, "get");
    const ctx: Ctx = {
      userId: 1,
      setUserId: () => {},
      users: [{ id: 1, name: "Test User", isAdmin: false }],
      refreshUsers: async () => {},
    };

    const { cleanup, container } = renderWithProviders(<UsersPage />, ctx);

    await act(async () => {
      await Promise.resolve();
    });

    expect(getSpy).not.toHaveBeenCalled();
    expect(container.textContent ?? "").toContain("Admins only");

    await cleanup();
  });
});
