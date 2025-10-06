import { afterEach, describe, expect, it, vi } from "vitest";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { MemoryRouter } from "react-router-dom";
import { createRoot } from "react-dom/client";
import { act } from "react-dom/test-utils";
import WishlistPage from "../WishlistPage";
import http from "@/lib/http";

vi.mock("@/state/useUser", () => ({
  useUser: () => ({
    userId: 42,
    setUserId: () => {},
    users: [],
    refreshUsers: () => Promise.resolve(),
  }),
}));

describe("WishlistPage", () => {
  afterEach(() => {
    vi.restoreAllMocks();
  });

  it("includes the name query parameter when a search term is present", async () => {
    const getMock = vi.spyOn(http, "get").mockResolvedValue({
      data: { items: [], total: 0, page: 1, pageSize: 50 },
    });

    const client = new QueryClient();
    const container = document.createElement("div");
    document.body.appendChild(container);
    const root = createRoot(container);

    await act(async () => {
      root.render(
        <MemoryRouter initialEntries={["/wishlist?q=bolt"]}>
          <QueryClientProvider client={client}>
            <WishlistPage />
          </QueryClientProvider>
        </MemoryRouter>
      );
    });

    await act(async () => {
      await Promise.resolve();
    });

    expect(getMock).toHaveBeenCalledWith(
      "user/42/wishlist",
      expect.objectContaining({
        params: expect.objectContaining({ name: "bolt" }),
      })
    );

    await act(async () => {
      root.unmount();
    });
    container.remove();
    client.clear();
  });
});
