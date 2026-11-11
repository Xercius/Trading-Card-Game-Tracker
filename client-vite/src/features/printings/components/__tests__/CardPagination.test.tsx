import { cleanup, render, screen, fireEvent } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, describe, expect, it, vi } from "vitest";
import { CardPagination } from "../CardPagination";

describe("CardPagination", () => {
  afterEach(() => {
    cleanup();
    vi.restoreAllMocks();
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

  it("opens page size options above a footer near the viewport bottom", async () => {
    const user = userEvent.setup();
    const onPageSizeChange = vi.fn();
    vi.spyOn(HTMLElement.prototype, "scrollHeight", "get").mockReturnValue(120);
    render(
      <CardPagination
        page={1}
        totalPages={5}
        pageSize={20}
        onPageChange={vi.fn()}
        onPageSizeChange={onPageSizeChange}
      />
    );
    const trigger = screen.getByRole("combobox", { name: "Cards per page" });
    vi.spyOn(trigger, "getBoundingClientRect").mockReturnValue(
      new DOMRect(100, window.innerHeight - 45, 120, 36)
    );
    await user.click(trigger);
    const listbox = screen.getByRole("listbox");
    const top = Number.parseFloat(listbox.style.top);
    expect(top).toBeGreaterThanOrEqual(8);
    expect(top + 120).toBeLessThan(window.innerHeight - 45);
    expect(listbox).toHaveStyle({ position: "fixed" });

    await user.click(screen.getByRole("option", { name: "100 / page" }));
    expect(onPageSizeChange).toHaveBeenCalledWith(100);
    expect(trigger).toHaveFocus();
  });

  it("constrains long page menus to available space and repositions on resize", async () => {
    const user = userEvent.setup();
    vi.spyOn(HTMLElement.prototype, "scrollHeight", "get").mockReturnValue(1200);
    render(
      <CardPagination
        page={1}
        totalPages={100}
        pageSize={20}
        onPageChange={vi.fn()}
        onPageSizeChange={vi.fn()}
      />
    );
    const trigger = screen.getByRole("combobox", { name: "Select page" });
    const rectSpy = vi
      .spyOn(trigger, "getBoundingClientRect")
      .mockReturnValue(new DOMRect(100, 100, 120, 36));
    await user.click(trigger);
    const listbox = screen.getByRole("listbox");
    expect(Number.parseFloat(listbox.style.maxHeight)).toBeLessThanOrEqual(240);
    rectSpy.mockReturnValue(new DOMRect(100, window.innerHeight - 45, 120, 36));
    fireEvent.resize(window);
    expect(Number.parseFloat(listbox.style.top)).toBeLessThan(window.innerHeight - 45);
    expect(Number.parseFloat(listbox.style.maxHeight)).toBeGreaterThan(0);
    expect(listbox).toHaveClass("overflow-auto");
  });
});
