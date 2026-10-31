import { cleanup, render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, describe, expect, it, vi } from "vitest";
import { CardPagination } from "../CardPagination";

describe("CardPagination", () => {
  afterEach(() => {
    cleanup();
  });
  it("disables first and previous buttons on the first page", () => {
    const onPageChange = vi.fn();
    const onPageSizeChange = vi.fn();

    render(
      <CardPagination
        page={1}
        totalPages={5}
        pageSize={20}
        onPageChange={onPageChange}
        onPageSizeChange={onPageSizeChange}
      />
    );

    expect(screen.getByRole("button", { name: "First page" })).toBeDisabled();
    expect(screen.getByRole("button", { name: "Previous page" })).toBeDisabled();
    expect(screen.getByRole("button", { name: "Next page" })).toBeEnabled();
    expect(screen.getByRole("button", { name: "Last page" })).toBeEnabled();
  });

  it("allows pagination controls to wrap on narrow screens", () => {
    const { container } = render(
      <CardPagination
        page={1}
        totalPages={5}
        pageSize={20}
        onPageChange={vi.fn()}
        onPageSizeChange={vi.fn()}
      />
    );

    expect(container.firstElementChild).toHaveClass("flex-wrap");
  });

  it("disables next and last buttons on the last page", () => {
    const onPageChange = vi.fn();
    const onPageSizeChange = vi.fn();

    render(
      <CardPagination
        page={5}
        totalPages={5}
        pageSize={20}
        onPageChange={onPageChange}
        onPageSizeChange={onPageSizeChange}
      />
    );

    expect(screen.getByRole("button", { name: "First page" })).toBeEnabled();
    expect(screen.getByRole("button", { name: "Previous page" })).toBeEnabled();
    expect(screen.getByRole("button", { name: "Next page" })).toBeDisabled();
    expect(screen.getByRole("button", { name: "Last page" })).toBeDisabled();
  });

  it("calls onPageChange with expected page on arrow clicks", async () => {
    const user = userEvent.setup();
    const onPageChange = vi.fn();
    const onPageSizeChange = vi.fn();

    render(
      <CardPagination
        page={3}
        totalPages={5}
        pageSize={20}
        onPageChange={onPageChange}
        onPageSizeChange={onPageSizeChange}
      />
    );

    await user.click(screen.getByRole("button", { name: "First page" }));
    expect(onPageChange).toHaveBeenCalledWith(1);

    await user.click(screen.getByRole("button", { name: "Previous page" }));
    expect(onPageChange).toHaveBeenCalledWith(2);

    await user.click(screen.getByRole("button", { name: "Next page" }));
    expect(onPageChange).toHaveBeenCalledWith(4);

    await user.click(screen.getByRole("button", { name: "Last page" }));
    expect(onPageChange).toHaveBeenCalledWith(5);
  });

  it("locks and disables page size dropdown in table view", () => {
    render(
      <CardPagination
        page={1}
        totalPages={3}
        pageSize={200}
        onPageChange={vi.fn()}
        onPageSizeChange={vi.fn()}
        isTableView={true}
      />
    );

    const pageSizeCombobox = screen.getByRole("combobox", { name: "Cards per page" });
    expect(pageSizeCombobox).toBeDisabled();
    expect(pageSizeCombobox).toHaveTextContent("200 / page");
  });

  it("allows selecting page size in grid view", async () => {
    const user = userEvent.setup();
    const onPageSizeChange = vi.fn();

    render(
      <CardPagination
        page={1}
        totalPages={5}
        pageSize={20}
        onPageChange={vi.fn()}
        onPageSizeChange={onPageSizeChange}
        isTableView={false}
      />
    );

    const pageSizeCombobox = screen.getByRole("combobox", { name: "Cards per page" });
    expect(pageSizeCombobox).toBeEnabled();
    expect(pageSizeCombobox).toHaveTextContent("20 / page");

    await user.click(pageSizeCombobox);
    const option40 = await screen.findByRole("option", { name: "40 / page" });
    await user.click(option40);

    expect(onPageSizeChange).toHaveBeenCalledWith(40);
  });
});
