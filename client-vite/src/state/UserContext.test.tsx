import { cleanup, render, screen } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";
import { MemoryRouter } from "react-router-dom";
import Header from "@/components/app/Header";
import http from "@/lib/http";
import { UserProvider } from "./UserProvider";
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

    render(
      <MemoryRouter initialEntries={["/cards"]}>
        <UserProvider>
          <Header />
          <Consumer />
        </UserProvider>
      </MemoryRouter>
    );

    expect(screen.getByTestId("user-id")).toHaveTextContent("1");
    expect(screen.getByTestId("user-names")).toHaveTextContent("Owner");
    expect(screen.getAllByText("Owner")).toHaveLength(2);
    expect(screen.queryByRole("combobox")).not.toBeInTheDocument();
    expect(screen.queryByRole("dialog")).not.toBeInTheDocument();
    expect(screen.queryByLabelText(/username|password/i)).not.toBeInTheDocument();
    expect(getSpy).not.toHaveBeenCalled();
    expect(postSpy).not.toHaveBeenCalled();
  });
});
