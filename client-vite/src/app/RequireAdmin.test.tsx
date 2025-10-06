import { describe, it, expect } from "vitest";
import { MemoryRouter, Route, Routes } from "react-router-dom";
import { createRoot } from "react-dom/client";
import { act } from "react-dom/test-utils";
import type { ReactElement } from "react";
import { RequireAdmin } from "./RequireAdmin";
import { UserContext, type Ctx } from "@/state/UserContext";

function render(element: ReactElement) {
  const container = document.createElement("div");
  document.body.appendChild(container);
  const root = createRoot(container);
  act(() => {
    root.render(element);
  });
  return {
    container,
    cleanup() {
      act(() => {
        root.unmount();
      });
      container.remove();
    },
  };
}

describe("RequireAdmin", () => {
  it("redirects non-admin users to /cards", async () => {
    const ctx: Ctx = {
      userId: 1,
      setUserId: () => {},
      users: [{ id: 1, name: "Test", isAdmin: false }],
      refreshUsers: async () => {},
    };

    const { container, cleanup } = render(
      <UserContext.Provider value={ctx}>
        <MemoryRouter initialEntries={["/admin/users"]}>
          <Routes>
            <Route
              path="/admin/users"
              element={
                <RequireAdmin>
                  <div>secret</div>
                </RequireAdmin>
              }
            />
            <Route path="/cards" element={<div>cards</div>} />
          </Routes>
        </MemoryRouter>
      </UserContext.Provider>
    );

    await act(async () => {
      await Promise.resolve();
    });

    expect(container.textContent).toContain("cards");

    cleanup();
  });
});
