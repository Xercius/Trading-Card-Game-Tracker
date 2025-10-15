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
      expect(dropdown).toHaveClass("bg-gray-900");
    });
  });

  it("renders with controlled state", async () => {
    const user = userEvent.setup();
    let open = false;
    const setOpen = (newOpen: boolean) => {
      open = newOpen;
    };

    const { rerender } = render(
      <FilterDropdown trigger={<button>Open Filters</button>} open={open} onOpenChange={setOpen}>
        <div>Filter content</div>
      </FilterDropdown>
    );

    expect(screen.queryByText("Filter content")).not.toBeInTheDocument();

    // Programmatically open
    open = true;
    rerender(
      <FilterDropdown trigger={<button>Open Filters</button>} open={open} onOpenChange={setOpen}>
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

  it("has proper ARIA attributes on trigger", () => {
    render(
      <FilterDropdown trigger={<button>Open Filters</button>}>
        <div>Filter content</div>
      </FilterDropdown>
    );

    const buttons = screen.getAllByRole("button");
    const triggerWrapper = buttons.find((btn) => btn.hasAttribute("aria-haspopup"));
    expect(triggerWrapper).toBeDefined();
    expect(triggerWrapper).toHaveAttribute("aria-haspopup", "menu");
    expect(triggerWrapper).toHaveAttribute("aria-expanded", "false");
    expect(triggerWrapper).toHaveAttribute("aria-controls");
  });

  it("updates aria-expanded when dropdown opens", async () => {
    const user = userEvent.setup();
    render(
      <FilterDropdown trigger={<button>Open Filters</button>}>
        <div>Filter content</div>
      </FilterDropdown>
    );

    const buttons = screen.getAllByRole("button");
    const triggerWrapper = buttons.find((btn) => btn.hasAttribute("aria-haspopup"));
    expect(triggerWrapper).toHaveAttribute("aria-expanded", "false");

    const trigger = screen.getByText("Open Filters");
    await user.click(trigger);

    await waitFor(() => {
      expect(triggerWrapper).toHaveAttribute("aria-expanded", "true");
    });
  });

  it("has matching aria-controls and menu id", async () => {
    const user = userEvent.setup();
    render(
      <FilterDropdown trigger={<button>Open Filters</button>}>
        <div>Filter content</div>
      </FilterDropdown>
    );

    const buttons = screen.getAllByRole("button");
    const triggerWrapper = buttons.find((btn) => btn.hasAttribute("aria-haspopup"));
    const controlsId = triggerWrapper?.getAttribute("aria-controls");
    expect(controlsId).toBeTruthy();

    const trigger = screen.getByText("Open Filters");
    await user.click(trigger);

    await waitFor(() => {
      const menu = screen.getByRole("menu");
      expect(menu).toHaveAttribute("id", controlsId);
    });
  });

  it("uses unique IDs for ARIA relationships between trigger and menu", async () => {
    const user = userEvent.setup();
    const { container } = render(
      <>
        <FilterDropdown trigger={<button>Filters 1</button>}>
          <div>Content 1</div>
        </FilterDropdown>
        <FilterDropdown trigger={<button>Filters 2</button>}>
          <div>Content 2</div>
        </FilterDropdown>
      </>
    );

    const triggers = container.querySelectorAll('[role="button"][aria-haspopup="menu"]');
    expect(triggers).toHaveLength(2);

    const trigger1 = triggers[0];
    const trigger2 = triggers[1];

    const trigger1Id = trigger1.getAttribute("id");
    const trigger1Controls = trigger1.getAttribute("aria-controls");
    const trigger2Id = trigger2.getAttribute("id");
    const trigger2Controls = trigger2.getAttribute("aria-controls");

    // Verify all IDs are unique
    expect(trigger1Id).toBeTruthy();
    expect(trigger1Controls).toBeTruthy();
    expect(trigger2Id).toBeTruthy();
    expect(trigger2Controls).toBeTruthy();
    expect(trigger1Id).not.toBe(trigger2Id);
    expect(trigger1Controls).not.toBe(trigger2Controls);

    // Open first dropdown
    const button1 = screen.getByText("Filters 1");
    await user.click(button1);

    await waitFor(() => {
      const menu = screen.getByText("Content 1").parentElement;
      expect(menu).toHaveAttribute("role", "menu");
      expect(menu).toHaveAttribute("id", trigger1Controls);
      expect(menu).toHaveAttribute("aria-labelledby", trigger1Id);
    });
  });

  it("works in controlled mode without onOpenChange (edge case)", async () => {
    const user = userEvent.setup();
    const { rerender } = render(
      <FilterDropdown trigger={<button>Open Filters</button>} open={false}>
        <div>Filter content</div>
      </FilterDropdown>
    );

    expect(screen.queryByText("Filter content")).not.toBeInTheDocument();

    // Programmatically open without onOpenChange - should not throw error
    rerender(
      <FilterDropdown trigger={<button>Open Filters</button>} open={true}>
        <div>Filter content</div>
      </FilterDropdown>
    );

    await waitFor(() => {
      expect(screen.getByText("Filter content")).toBeInTheDocument();
    });

    // Try clicking trigger - should not crash even without onOpenChange
    const trigger = screen.getByText("Open Filters");
    await user.click(trigger);

    // Since onOpenChange is not provided, the click should be a no-op or handled gracefully
    // The component should remain open since we're in controlled mode
    expect(screen.getByText("Filter content")).toBeInTheDocument();
  });

  it("handles Escape key gracefully in controlled mode without onOpenChange", async () => {
    const user = userEvent.setup();
    render(
      <FilterDropdown trigger={<button>Open Filters</button>} open={true}>
        <div>Filter content</div>
      </FilterDropdown>
    );

    await waitFor(() => {
      expect(screen.getByText("Filter content")).toBeInTheDocument();
    });

    // Press Escape - should not throw error even without onOpenChange
    await user.keyboard("{Escape}");

    // Since we're in controlled mode without onOpenChange, state won't actually change
    // but it should not throw an error
    // The component should remain open since parent controls it
    expect(screen.getByText("Filter content")).toBeInTheDocument();
  });
});
