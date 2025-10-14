import type { ReactNode } from "react";
import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { act } from "react-dom/test-utils";
import { createRoot } from "react-dom/client";
import * as httpMod from "@/lib/http";
import { UserProvider } from "./UserProvider";
import { useUser } from "./useUser";

type AxiosGet = (typeof httpMod.default)["get"];
type AxiosPost = (typeof httpMod.default)["post"];

type MockResponse<T> = { data: T };

function Consumer() {
  const { userId, users } = useUser();
  return (
    <div>
      <div data-testid="user-id">{userId ?? "null"}</div>
      <div data-testid="user-count">{users.length}</div>
      <div data-testid="user-names">{users.map((u) => u.name).join(",") || ""}</div>
    </div>
  );
}

/**
 * Wait for a condition to be true, checking periodically.
 * This replaces arbitrary timeouts and flush() chains with deterministic waits.
 */
async function waitFor(
  condition: () => boolean,
  options: { timeout?: number; interval?: number } = {}
): Promise<void> {
  const { timeout = 5000, interval = 50 } = options;
  const startTime = Date.now();

  while (!condition()) {
    if (Date.now() - startTime > timeout) {
      throw new Error("waitFor timeout: condition not met");
    }
    await new Promise((resolve) => setTimeout(resolve, interval));
  }
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
      return Promise.reject(new Error(`Unexpected url: ${url}`));
    });
    window.localStorage.setItem("authToken", "stale-token");

    const setSpy = vi.spyOn(httpMod, "setHttpAccessToken").mockImplementation(() => {});
    const view = renderWithProvider(<Consumer />);
    await view.render();

    // Wait for the refresh failure to complete and user state to be cleared
    await waitFor(() => setSpy.mock.calls.length > 0);
    expect(setSpy).toHaveBeenLastCalledWith(null);
    expect(window.localStorage.getItem("authToken")).toBeNull();

    const value = view.container.querySelector('[data-testid="user-id"]');
    expect(value?.textContent).toBe("null");

    await view.cleanup();
  });

  it("renders a login form when unauthenticated", async () => {
    const getMock = vi.spyOn(httpMod.default, "get") as unknown as vi.MockedFunction<AxiosGet>;
    getMock.mockResolvedValue({ data: {} } as MockResponse<unknown>);

    const view = renderWithProvider(<Consumer />);
    await view.render();

    // Wait for login form to be rendered in the DOM
    await waitFor(() => document.querySelector("#login-username") !== null);

    // Dialog component uses a portal, so query from document.body
    const username = document.querySelector<HTMLInputElement>("#login-username");
    const password = document.querySelector<HTMLInputElement>("#login-password");
    const button = document.querySelector<HTMLButtonElement>('button[type="submit"]');

    expect(username).not.toBeNull();
    expect(password).not.toBeNull();
    expect(button?.textContent).toContain("Sign in");
    expect(getMock).not.toHaveBeenCalled();

    await view.cleanup();
  });

  it("logs in successfully and populates users", async () => {
    const getMock = vi.spyOn(httpMod.default, "get") as unknown as vi.MockedFunction<AxiosGet>;
    getMock.mockImplementation((url: string) => {
      if (url === "user/me") {
        return Promise.resolve({
          data: { id: 1, username: "alice", displayName: "Alice", isAdmin: true },
        });
      }
      if (url === "admin/users") {
        return Promise.resolve({
          data: [
            { id: 1, username: "alice", displayName: "Alice", isAdmin: true },
            { id: 2, username: "bob", displayName: "Bob", isAdmin: false },
          ],
        });
      }
      throw new Error(`Unexpected url: ${url}`);
    });

    const postMock = vi.spyOn(httpMod.default, "post") as unknown as vi.MockedFunction<AxiosPost>;
    postMock.mockImplementation((url: string, body: unknown) => {
      if (url === "auth/login") {
        expect(body).toEqual({ username: "alice", password: "Password123!" });
        return Promise.resolve({
          data: {
            accessToken: "token-1",
            expiresAtUtc: new Date().toISOString(),
            user: { id: 1, username: "alice", displayName: "Alice", isAdmin: true },
          },
        } as MockResponse<unknown>);
      }
      throw new Error(`Unexpected url: ${url}`);
    });

    const setSpy = vi.spyOn(httpMod, "setHttpAccessToken").mockImplementation(() => {});

    const view = renderWithProvider(<Consumer />);
    await view.render();

    // Wait for login form to be rendered
    await waitFor(() => document.querySelector("#login-username") !== null);

    // Dialog component uses a portal, so query from document.body
    const username = document.querySelector<HTMLInputElement>("#login-username");
    const password = document.querySelector<HTMLInputElement>("#login-password");
    const form = document.querySelector<HTMLFormElement>("form");
    const submitButton = document.querySelector<HTMLButtonElement>('button[type="submit"]');
    expect(username).not.toBeNull();
    expect(password).not.toBeNull();
    expect(form).not.toBeNull();

    await act(async () => {
      if (username) {
        // Use React's internal setter to properly update controlled input
        const nativeInputValueSetter = Object.getOwnPropertyDescriptor(
          window.HTMLInputElement.prototype,
          "value"
        )?.set;
        nativeInputValueSetter?.call(username, "alice");
        username.dispatchEvent(new Event("input", { bubbles: true }));
      }
      if (password) {
        const nativeInputValueSetter = Object.getOwnPropertyDescriptor(
          window.HTMLInputElement.prototype,
          "value"
        )?.set;
        nativeInputValueSetter?.call(password, "Password123!");
        password.dispatchEvent(new Event("input", { bubbles: true }));
      }
    });

    await act(async () => {
      submitButton?.click();
    });

    // Wait for login to complete and check HTTP calls
    await waitFor(() => postMock.mock.calls.length > 0);
    expect(postMock).toHaveBeenCalledWith("auth/login", { username: "alice", password: "Password123!" });
    expect(setSpy).toHaveBeenCalledWith("token-1");
    expect(window.localStorage.getItem("authToken")).toBe("token-1");

    // Wait for user data to be populated in the DOM
    await waitFor(() => {
      const count = view.container.querySelector('[data-testid="user-count"]');
      return count?.textContent === "2";
    });

    // Check that setHttpAccessToken was called and not cleared
    const lastCall = setSpy.mock.calls[setSpy.mock.calls.length - 1];
    expect(lastCall?.[0]).toBe("token-1");

    const count = view.container.querySelector('[data-testid="user-count"]');
    expect(count?.textContent).toBe("2");

    const names = view.container.querySelector('[data-testid="user-names"]');
    expect(names?.textContent).toBe("Alice,Bob");

    await view.cleanup();
  });
});
