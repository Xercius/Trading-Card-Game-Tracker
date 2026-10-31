import { cleanup, render, screen } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { afterEach, describe, expect, it, vi } from "vitest";
import { MemoryRouter } from "react-router-dom";
import Header from "@/components/app/Header";
import http from "@/lib/http";
import { UserProvider } from "./UserProvider";
import { GameProvider } from "./GameProvider";
import { useUser } from "./useUser";

function Consumer() {
  const { userId, users } = useUser();
  return (
    <div>
      <div data-testid="user-id">{userId}</div>
      <div data-testid="user-names">{users.map((user) => user.name).join(",")}</div>
    </div>
  );
}

afterEach(() => {
  cleanup();
  vi.restoreAllMocks();
});

describe("UserProvider", () => {
  it("opens directly as the seeded owner without regular login or impersonation requests", () => {
    const getSpy = vi.spyOn(http, "get");
    const postSpy = vi.spyOn(http, "post");

    const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    render(
      <QueryClientProvider client={queryClient}>
        <MemoryRouter initialEntries={["/cards"]}>
          <UserProvider>
            <GameProvider>
              <Header />
              <Consumer />
            </GameProvider>
          </UserProvider>
        </MemoryRouter>
      </QueryClientProvider>
    );

    expect(screen.getByTestId("user-id")).toHaveTextContent("1");
    expect(screen.getByTestId("user-names")).toHaveTextContent("Owner");
    expect(screen.getAllByText("Owner")).toHaveLength(2);
    expect(screen.queryByRole("combobox", { name: /user/i })).not.toBeInTheDocument();
    expect(screen.queryByRole("dialog")).not.toBeInTheDocument();
    expect(screen.queryByLabelText(/username|password/i)).not.toBeInTheDocument();
    const authGetCalls = getSpy.mock.calls.filter(([url]) =>
      typeof url === "string" ? /auth|user|login/i.test(url) : false
    );
    expect(authGetCalls).toHaveLength(0);
    expect(postSpy).not.toHaveBeenCalled();
  });
});
