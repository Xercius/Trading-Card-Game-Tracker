import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { createRoot } from "react-dom/client";
import { act } from "react-dom/test-utils";
import { UserProvider } from "./UserProvider";
import { useUser } from "./useUser";
import * as httpMod from "@/lib/http";

function Consumer() {
  const { userId } = useUser();
  return <div data-testid="user-id">{userId ?? "null"}</div>;
}

describe("UserProvider refresh error handling", () => {
  beforeEach(() => {
    vi.spyOn(httpMod.default, "get").mockRejectedValue(new Error("401"));
    window.localStorage.setItem("userId", "999");
  });

  afterEach(() => {
    vi.restoreAllMocks();
    window.localStorage.clear();
  });

  it("clears userId and storage after refresh failure", async () => {
    const client = new QueryClient();
    const setSpy = vi.spyOn(httpMod, "setHttpUserId").mockImplementation(() => {});
    const container = document.createElement("div");
    document.body.appendChild(container);
    const root = createRoot(container);

    await act(async () => {
      root.render(
        <QueryClientProvider client={client}>
          <UserProvider>
            <Consumer />
          </UserProvider>
        </QueryClientProvider>
      );
    });

    await act(async () => {
      await Promise.resolve();
    });

    expect(setSpy).toHaveBeenLastCalledWith(null);
    expect(window.localStorage.getItem("userId")).toBeNull();
    const target = container.querySelector('[data-testid="user-id"]');
    expect(target?.textContent).toBe("null");

    await act(async () => {
      root.unmount();
    });
    container.remove();
    client.clear();
  });
});
