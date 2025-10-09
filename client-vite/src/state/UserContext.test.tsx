import type { ReactNode } from "react";
import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { act } from "react-dom/test-utils";
import { createRoot } from "react-dom/client";
import * as httpMod from "@/lib/http";
import { UserProvider } from "./UserProvider";
import { useUser } from "./useUser";

type AxiosGet = (typeof httpMod.default)["get"];

type MockResponse<T> = { data: T };

function Consumer() {
  const { userId } = useUser();
  return <div data-testid="user-id">{userId ?? "null"}</div>;
}

function renderWithProvider(children: ReactNode) {
  const client = new QueryClient();
  const container = document.createElement("div");
  document.body.appendChild(container);
  const root = createRoot(container);

  return {
    client,
    container,
    root,
    async render() {
      await act(async () => {
        root.render(
          <QueryClientProvider client={client}>
            <UserProvider>{children}</UserProvider>
          </QueryClientProvider>
        );
      });
    },
    async flush() {
      await act(async () => {
        await Promise.resolve();
      });
    },
    async cleanup() {
      await act(async () => {
        root.unmount();
      });
      container.remove();
      client.clear();
    },
  };
}

describe("UserProvider", () => {
  beforeEach(() => {
    window.localStorage.clear();
  });

  afterEach(() => {
    vi.restoreAllMocks();
    window.localStorage.clear();
  });

  it("clears userId and storage after refresh failure", async () => {
    const getMock = vi.spyOn(httpMod.default, "get") as unknown as vi.MockedFunction<AxiosGet>;
    getMock.mockImplementation((url: string) => {
      if (url === "user/me") {
        return Promise.reject(new Error("401"));
      }
      if (url === "user/list") {
        return Promise.resolve({ data: [] } as MockResponse<unknown>);
      }
      return Promise.reject(new Error(`Unexpected url: ${url}`));
    });
    window.localStorage.setItem("userId", "999");

    const setSpy = vi.spyOn(httpMod, "setHttpUserId").mockImplementation(() => {});
    const view = renderWithProvider(<Consumer />);
    await view.render();
    await view.flush();

    expect(setSpy).toHaveBeenLastCalledWith(null);
    expect(window.localStorage.getItem("userId")).toBeNull();

    const value = view.container.querySelector('[data-testid="user-id"]');
    expect(value?.textContent).toBe("null");

    await view.cleanup();
  });

  it("omits X-User-Id header when no user is selected", async () => {
    const getMock = vi.spyOn(httpMod.default, "get") as unknown as vi.MockedFunction<AxiosGet>;
    getMock.mockImplementation((url: string) => {
      if (url === "user/list") {
        expect(httpMod.__debugGetCurrentUserId()).toBeNull();
        const common = httpMod.default.defaults.headers.common as Record<string, string | undefined>;
        expect(common["X-User-Id"]).toBeUndefined();
        return Promise.resolve({ data: [] } as MockResponse<unknown>);
      }
      return Promise.reject(new Error(`Unexpected url: ${url}`));
    });

    const view = renderWithProvider(<Consumer />);
    await view.render();
    await view.flush();

    expect(getMock).toHaveBeenCalledWith("user/list");

    await view.cleanup();
  });

  it("selecting a user sets the header and refreshes user data", async () => {
    const getMock = vi.spyOn(httpMod.default, "get") as unknown as vi.MockedFunction<AxiosGet>;
    const sequence = vi.fn<Promise<MockResponse<unknown>>, [string]>((url: string) => {
      if (url === "user/list") {
        return Promise.resolve({
          data: [
            { id: 1, username: "alice", displayName: "Alice" },
            { id: 2, username: "bob", displayName: "Bob" },
          ],
        });
      }
      if (url === "user/me") {
        return Promise.resolve({
          data: { id: 1, username: "alice", displayName: "Alice", isAdmin: false },
        });
      }
      throw new Error(`Unexpected url: ${url}`);
    });
    getMock.mockImplementation(sequence);

    const setSpy = vi.spyOn(httpMod, "setHttpUserId").mockImplementation(() => {});

    const view = renderWithProvider(<Consumer />);
    await view.render();
    await view.flush();

    const button = view.container.querySelector('[data-testid="user-option-1"]');
    expect(button).not.toBeNull();

    await act(async () => {
      button?.dispatchEvent(new MouseEvent("click", { bubbles: true }));
    });

    await view.flush();

    expect(setSpy).toHaveBeenCalledWith(1);
    expect(window.localStorage.getItem("userId")).toBe("1");
    expect(getMock).toHaveBeenCalledWith("user/me");
    expect(httpMod.__debugGetCurrentUserId()).toBe(1);

    await view.cleanup();
  });
});
