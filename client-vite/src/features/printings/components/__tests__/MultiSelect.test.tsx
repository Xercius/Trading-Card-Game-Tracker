import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it, vi } from "vitest";
import { MultiSelect } from "../MultiSelect";

describe("MultiSelect", () => {
  it("renders the open filter menu with an opaque background", async () => {
    const user = userEvent.setup();
    render(<MultiSelect label="Set" values={[]} options={["Set A"]} onChange={vi.fn()} />);

    await user.click(screen.getByRole("button", { name: "Set (All)" }));

    const menu = screen.getByRole("listbox").parentElement;
    expect(menu).toHaveClass("bg-[#242424]");
  });

  it("supports selecting All or individual options", async () => {
    const user = userEvent.setup();
    const onChange = vi.fn();
    render(
      <MultiSelect
        label="Aspects"
        values={["Heroism"]}
        options={["Heroism", "Command"]}
        onChange={onChange}
      />
    );

    await user.click(screen.getByRole("button", { name: "Aspects (1)" }));
    await user.click(screen.getByRole("option", { name: "All" }));

    expect(onChange).toHaveBeenCalledWith([]);
  });
});
