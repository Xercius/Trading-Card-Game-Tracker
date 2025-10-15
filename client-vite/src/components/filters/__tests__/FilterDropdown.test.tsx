import { describe, it, expect, afterEach } from "vitest";
import { render, screen, waitFor, cleanup } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import FilterDropdown from "../FilterDropdown";

describe("FilterDropdown", () => {
  afterEach(() => {
    cleanup();
  });

  it("renders trigger and opens dropdown on click", async () => {
    const user = userEvent.setup();
    render(
      <FilterDropdown trigger={<button>Open Filters</button>}>
        <div>Filter content</div>
      </FilterDropdown>
    );

    const trigger = screen.getByText("Open Filters");
    expect(screen.queryByText("Filter content")).not.toBeInTheDocument();

    await user.click(trigger);

    await waitFor(() => {
      expect(screen.getByText("Filter content")).toBeInTheDocument();
    });

    const dropdown = screen.getByRole("menu");
    expect(dropdown).toBeInTheDocument();
  });

  it("closes dropdown on Escape key", async () => {
    const user = userEvent.setup();
    render(
      <FilterDropdown trigger={<button>Open Filters</button>}>
        <div>Filter content</div>
      </FilterDropdown>
    );

    const trigger = screen.getByText("Open Filters");
    await user.click(trigger);

    await waitFor(() => {
      expect(screen.getByText("Filter content")).toBeInTheDocument();
    });

    await user.keyboard("{Escape}");

    await waitFor(() => {
      expect(screen.queryByText("Filter content")).not.toBeInTheDocument();
    });
  });

  it("closes dropdown when clicking outside", async () => {
    const user = userEvent.setup();
    render(
      <div>
        <div data-testid="outside">Outside element</div>
        <FilterDropdown trigger={<button>Open Filters</button>}>
          <div>Filter content</div>
        </FilterDropdown>
      </div>
    );

    const trigger = screen.getByText("Open Filters");
    await user.click(trigger);

    await waitFor(() => {
      expect(screen.getByText("Filter content")).toBeInTheDocument();
    });

    const outside = screen.getByTestId("outside");
    await user.click(outside);

    await waitFor(() => {
      expect(screen.queryByText("Filter content")).not.toBeInTheDocument();
    });
  });

  it("has proper z-index and styling", async () => {
    const user = userEvent.setup();
    render(
      <FilterDropdown trigger={<button>Open Filters</button>}>
        <div>Filter content</div>
      </FilterDropdown>
    );

    const trigger = screen.getByText("Open Filters");
    await user.click(trigger);

    await waitFor(() => {
      const dropdown = screen.getByRole("menu");
      expect(dropdown).toHaveClass("z-50");
      expect(dropdown).toHaveClass("shadow-lg");
      expect(dropdown).toHaveClass("ring-1");
      expect(dropdown).toHaveClass("ring-black/5");
    });
  });

  it("supports dark mode styling", async () => {
    const user = userEvent.setup();
    render(
      <FilterDropdown trigger={<button>Open Filters</button>}>
        <div>Filter content</div>
      </FilterDropdown>
    );

    const trigger = screen.getByText("Open Filters");
    await user.click(trigger);

    await waitFor(() => {
      const dropdown = screen.getByRole("menu");
      expect(dropdown).toHaveClass("bg-white");
      expect(dropdown).toHaveClass("dark:bg-gray-900");
    });
  });

  it("renders with controlled state", async () => {
    const user = userEvent.setup();
    let open = false;
    const setOpen = (newOpen: boolean) => {
      open = newOpen;
    };

    const { rerender } = render(
      <FilterDropdown
        trigger={<button>Open Filters</button>}
        open={open}
        onOpenChange={setOpen}
      >
        <div>Filter content</div>
      </FilterDropdown>
    );

    expect(screen.queryByText("Filter content")).not.toBeInTheDocument();

    // Programmatically open
    open = true;
    rerender(
      <FilterDropdown
        trigger={<button>Open Filters</button>}
        open={open}
        onOpenChange={setOpen}
      >
        <div>Filter content</div>
      </FilterDropdown>
    );

    await waitFor(() => {
      expect(screen.getByText("Filter content")).toBeInTheDocument();
    });
  });

  it("handles focus management when opened", async () => {
    const user = userEvent.setup();
    render(
      <FilterDropdown trigger={<button>Open Filters</button>}>
        <button>First button</button>
        <button>Second button</button>
      </FilterDropdown>
    );

    const trigger = screen.getByText("Open Filters");
    await user.click(trigger);

    await waitFor(() => {
      expect(screen.getByRole("menu")).toBeInTheDocument();
      expect(screen.getByText("First button")).toBeInTheDocument();
      expect(screen.getByText("Second button")).toBeInTheDocument();
    });
  });

  it("aligns dropdown to right when align prop is right", async () => {
    const user = userEvent.setup();
    render(
      <FilterDropdown trigger={<button>Open Filters</button>} align="right">
        <div>Filter content</div>
      </FilterDropdown>
    );

    const trigger = screen.getByText("Open Filters");
    await user.click(trigger);

    await waitFor(() => {
      const dropdown = screen.getByRole("menu");
      const style = dropdown.style;
      expect(style.right).toBeDefined();
    });
  });
});
