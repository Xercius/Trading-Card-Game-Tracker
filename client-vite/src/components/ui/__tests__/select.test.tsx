import { describe, it, expect, vi, afterEach } from "vitest";
import { render, screen, waitFor, cleanup } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { Select, SelectTrigger, SelectValue, SelectContent, SelectItem } from "../select";
import { cssEscapeId } from "@/test/utils/cssEscape";

describe("Select", () => {
  afterEach(() => {
    cleanup();
  });

  it("renders trigger and opens dropdown on click", async () => {
    const user = userEvent.setup();
    render(
      <Select>
        <SelectTrigger>
          <SelectValue placeholder="Select option" />
        </SelectTrigger>
        <SelectContent>
          <SelectItem value="option1">Option 1</SelectItem>
          <SelectItem value="option2">Option 2</SelectItem>
        </SelectContent>
      </Select>
    );

    const trigger = screen.getByRole("combobox");
    expect(trigger).toHaveAttribute("aria-expanded", "false");

    await user.click(trigger);

    await waitFor(() => {
      expect(trigger).toHaveAttribute("aria-expanded", "true");
    });

    expect(screen.getByRole("listbox")).toBeInTheDocument();
    expect(screen.getByRole("option", { name: "Option 1" })).toBeInTheDocument();
    expect(screen.getByRole("option", { name: "Option 2" })).toBeInTheDocument();
  });

  it("closes dropdown on Escape key", async () => {
    const user = userEvent.setup();
    render(
      <Select>
        <SelectTrigger>
          <SelectValue placeholder="Select option" />
        </SelectTrigger>
        <SelectContent>
          <SelectItem value="option1">Option 1</SelectItem>
        </SelectContent>
      </Select>
    );

    const trigger = screen.getByRole("combobox");
    await user.click(trigger);

    await waitFor(() => {
      expect(screen.getByRole("listbox")).toBeInTheDocument();
    });

    await user.keyboard("{Escape}");

    await waitFor(() => {
      expect(screen.queryByRole("listbox")).not.toBeInTheDocument();
    });
  });

  it("selects option on click and closes dropdown", async () => {
    const user = userEvent.setup();
    const handleChange = vi.fn();

    render(
      <Select onValueChange={handleChange}>
        <SelectTrigger>
          <SelectValue placeholder="Select option" />
        </SelectTrigger>
        <SelectContent>
          <SelectItem value="option1">Option 1</SelectItem>
          <SelectItem value="option2">Option 2</SelectItem>
        </SelectContent>
      </Select>
    );

    const trigger = screen.getByRole("combobox");
    await user.click(trigger);

    await waitFor(() => {
      expect(screen.getByRole("listbox")).toBeInTheDocument();
    });

    const option1 = screen.getByRole("option", { name: "Option 1" });
    await user.click(option1);

    expect(handleChange).toHaveBeenCalledWith("option1");

    await waitFor(() => {
      expect(screen.queryByRole("listbox")).not.toBeInTheDocument();
    });
  });

  it("supports keyboard navigation with arrow keys", async () => {
    const user = userEvent.setup({ delay: null });
    render(
      <Select>
        <SelectTrigger>
          <SelectValue placeholder="Select option" />
        </SelectTrigger>
        <SelectContent>
          <SelectItem value="option1">Option 1</SelectItem>
          <SelectItem value="option2">Option 2</SelectItem>
          <SelectItem value="option3">Option 3</SelectItem>
        </SelectContent>
      </Select>
    );

    const trigger = screen.getByRole("combobox");
    await user.click(trigger);

    await waitFor(() => {
      expect(screen.getByRole("listbox")).toBeInTheDocument();
    });

    // Verify all options are present and keyboard events don't cause errors
    const options = screen.getAllByRole("option");
    expect(options).toHaveLength(3);

    // Keyboard navigation should work without errors
    await user.keyboard("{ArrowDown}");
    await user.keyboard("{ArrowUp}");
    await user.keyboard("{Home}");
    await user.keyboard("{End}");

    // Listbox should still be visible
    expect(screen.getByRole("listbox")).toBeInTheDocument();
  });

  it("keyboard navigation works immediately after opening dropdown", async () => {
    const user = userEvent.setup({ delay: null });
    render(
      <Select>
        <SelectTrigger>
          <SelectValue placeholder="Select option" />
        </SelectTrigger>
        <SelectContent>
          <SelectItem value="option1">Option 1</SelectItem>
          <SelectItem value="option2">Option 2</SelectItem>
          <SelectItem value="option3">Option 3</SelectItem>
        </SelectContent>
      </Select>
    );

    const trigger = screen.getByRole("combobox");
    await user.click(trigger);

    await waitFor(() => {
      expect(screen.getByRole("listbox")).toBeInTheDocument();
    });

    const options = screen.getAllByRole("option");

    // Wait for first option to be focused automatically
    await waitFor(() => {
      expect(options[0]).toHaveFocus();
    });

    // ArrowDown should move to second option
    await user.keyboard("{ArrowDown}");
    await waitFor(() => {
      expect(options[1]).toHaveFocus();
    });

    // ArrowDown should move to third option
    await user.keyboard("{ArrowDown}");
    await waitFor(() => {
      expect(options[2]).toHaveFocus();
    });

    // Home should move to first option
    await user.keyboard("{Home}");
    await waitFor(() => {
      expect(options[0]).toHaveFocus();
    });

    // End should move to last option
    await user.keyboard("{End}");
    await waitFor(() => {
      expect(options[2]).toHaveFocus();
    });

    // ArrowUp should move to second option (wrapping from last)
    await user.keyboard("{ArrowUp}");
    await waitFor(() => {
      expect(options[1]).toHaveFocus();
    });
  });

  it("renders with proper accessibility attributes", async () => {
    const user = userEvent.setup();
    render(
      <Select>
        <SelectTrigger>
          <SelectValue placeholder="Select option" />
        </SelectTrigger>
        <SelectContent>
          <SelectItem value="option1">Option 1</SelectItem>
        </SelectContent>
      </Select>
    );

    const trigger = screen.getByRole("combobox");
    expect(trigger).toHaveAttribute("aria-haspopup", "listbox");
    expect(trigger).toHaveAttribute("aria-expanded", "false");

    await user.click(trigger);

    await waitFor(() => {
      const listbox = screen.getByRole("listbox");
      expect(listbox).toBeInTheDocument();
      expect(trigger).toHaveAttribute("aria-expanded", "true");

      // Verify ARIA relationship: trigger controls the listbox
      const ariaControls = trigger.getAttribute("aria-controls");
      expect(ariaControls).toBeTruthy();
      expect(listbox).toHaveAttribute("id", ariaControls);

      // Verify ARIA relationship: listbox is labeled by the trigger
      const triggerId = trigger.getAttribute("id");
      expect(triggerId).toBeTruthy();
      expect(listbox).toHaveAttribute("aria-labelledby", triggerId);
    });

    const option = screen.getByRole("option", { name: "Option 1" });
    expect(option).toHaveAttribute("aria-selected");
  });

  it("uses unique IDs for ARIA relationships between trigger and listbox", async () => {
    const user = userEvent.setup();
    render(
      <>
        <Select>
          <SelectTrigger>
            <SelectValue placeholder="Select 1" />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="a">A</SelectItem>
          </SelectContent>
        </Select>
        <Select>
          <SelectTrigger>
            <SelectValue placeholder="Select 2" />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="b">B</SelectItem>
          </SelectContent>
        </Select>
      </>
    );

    const triggers = screen.getAllByRole("combobox");
    expect(triggers).toHaveLength(2);

    // Open first select
    await user.click(triggers[0]);

    await waitFor(() => {
      const trigger1Id = triggers[0].getAttribute("id");
      const trigger1Controls = triggers[0].getAttribute("aria-controls");

      expect(trigger1Id).toBeTruthy();
      expect(trigger1Controls).toBeTruthy();

      // listbox is portaled to document.body - use role query
      const listbox = screen.getByRole("listbox");
      expect(listbox).toBeInTheDocument();
      expect(listbox).toHaveAttribute("id", trigger1Controls);
      expect(listbox).toHaveAttribute("aria-labelledby", trigger1Id);

      // Verify querySelector works with React-generated IDs (which may contain colons)
      // Must escape IDs for querySelector, or use getElementById
      const listboxById = document.getElementById(trigger1Controls!);
      expect(listboxById).toBe(listbox);

      // Alternatively, using querySelector with escaped ID
      const listboxBySelector = document.querySelector(`#${cssEscapeId(trigger1Controls!)}`);
      expect(listboxBySelector).toBe(listbox);
    });

    // Close first and open second
    await user.keyboard("{Escape}");
    await user.click(triggers[1]);

    await waitFor(() => {
      const trigger2Id = triggers[1].getAttribute("id");
      const trigger2Controls = triggers[1].getAttribute("aria-controls");

      expect(trigger2Id).toBeTruthy();
      expect(trigger2Controls).toBeTruthy();

      // IDs should be different
      expect(trigger2Id).not.toBe(triggers[0].getAttribute("id"));
      expect(trigger2Controls).not.toBe(triggers[0].getAttribute("aria-controls"));

      // listbox is portaled to document.body - use role query
      const listbox = screen.getByRole("listbox");
      expect(listbox).toBeInTheDocument();
      expect(listbox).toHaveAttribute("id", trigger2Controls);
      expect(listbox).toHaveAttribute("aria-labelledby", trigger2Id);

      // Verify querySelector works with React-generated IDs (which may contain colons)
      // Must escape IDs for querySelector, or use getElementById
      const listboxById = document.getElementById(trigger2Controls!);
      expect(listboxById).toBe(listbox);

      // Alternatively, using querySelector with escaped ID
      const listboxBySelector = document.querySelector(`#${cssEscapeId(trigger2Controls!)}`);
      expect(listboxBySelector).toBe(listbox);
    });
  });

  it("has proper z-index for dropdown", async () => {
    const user = userEvent.setup();
    render(
      <Select>
        <SelectTrigger>
          <SelectValue placeholder="Select option" />
        </SelectTrigger>
        <SelectContent>
          <SelectItem value="option1">Option 1</SelectItem>
        </SelectContent>
      </Select>
    );

    const trigger = screen.getByRole("combobox");
    await user.click(trigger);

    await waitFor(() => {
      const listbox = screen.getByRole("listbox");
      expect(listbox).toHaveClass("z-50");
    });
  });

  it("displays selected value after opening once", async () => {
    const user = userEvent.setup();
    render(
      <Select defaultValue="option2">
        <SelectTrigger>
          <SelectValue placeholder="Select option" />
        </SelectTrigger>
        <SelectContent>
          <SelectItem value="option1">Option 1</SelectItem>
          <SelectItem value="option2">Option 2</SelectItem>
        </SelectContent>
      </Select>
    );

    const trigger = screen.getByRole("combobox");

    // Open dropdown so items can register their labels
    await user.click(trigger);
    await waitFor(() => {
      expect(screen.getByRole("listbox")).toBeInTheDocument();
    });

    // Close it
    await user.keyboard("{Escape}");
    await waitFor(() => {
      expect(screen.queryByRole("listbox")).not.toBeInTheDocument();
    });

    // Now the label should be set
    expect(trigger).toHaveTextContent("Option 2");
  });

  it("handles React-generated IDs with special characters in querySelector", async () => {
    const user = userEvent.setup();
    render(
      <Select>
        <SelectTrigger>
          <SelectValue placeholder="Select option" />
        </SelectTrigger>
        <SelectContent>
          <SelectItem value="option1">Option 1</SelectItem>
        </SelectContent>
      </Select>
    );

    const trigger = screen.getByRole("combobox");
    await user.click(trigger);

    await waitFor(() => {
      const listbox = screen.getByRole("listbox");
      const listboxId = listbox.getAttribute("id");

      expect(listboxId).toBeTruthy();

      // Method 1 (RECOMMENDED): Use getElementById - always safe, no escaping needed
      const elementById = document.getElementById(listboxId!);
      expect(elementById).toBe(listbox);

      // Method 2: Use cssEscapeId for querySelector when ID may contain special chars
      // React's useId() can generate IDs like ":r1:-listbox" which need escaping
      const elementBySelector = document.querySelector(`#${cssEscapeId(listboxId!)}`);
      expect(elementBySelector).toBe(listbox);

      // This test demonstrates that both methods work even if the ID contains
      // special characters like colons (e.g., ":r1:-listbox" from React's useId)
    });
  });
});
