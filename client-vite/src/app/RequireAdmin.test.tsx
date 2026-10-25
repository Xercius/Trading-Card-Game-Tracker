import { describe, it, expect } from "vitest";
import { MemoryRouter, Route, Routes } from "react-router-dom";
import { waitFor } from "@testing-library/react";
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
      users: [{ id: 1, name: "Test", isAdmin: false }],
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

    await waitFor(() => expect(container.textContent).toContain("cards"));

    cleanup();
  });

  it("allows the single owner to access the import page", () => {
    const ctx: Ctx = {
      userId: 1,
      users: [{ id: 1, name: "Owner", isAdmin: false }],
    };

    const { container, cleanup } = render(
      <UserContext.Provider value={ctx}>
        <MemoryRouter initialEntries={["/admin/import"]}>
          <Routes>
            <Route
              path="/admin/import"
              element={
                <RequireAdmin allowOwner>
                  <div>import</div>
                </RequireAdmin>
              }
            />
            <Route path="/cards" element={<div>cards</div>} />
          </Routes>
        </MemoryRouter>
      </UserContext.Provider>
    );

    expect(container.textContent).toContain("import");
    cleanup();
  });
});
