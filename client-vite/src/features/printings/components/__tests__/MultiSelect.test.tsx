import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it, vi } from "vitest";
import { MultiSelect } from "../MultiSelect";

describe("MultiSelect", () => {
  it("renders the open filter menu with an opaque background", async () => {
    const user = userEvent.setup();
    render(<MultiSelect label="Set" values={[]} options={["Set A"]} onChange={vi.fn()} />);

    await user.click(screen.getByRole("button", { name: "Set" }));

    const menu = screen.getByRole("listbox").parentElement;
    expect(menu).toHaveClass("bg-[#242424]");
  });

  it("supports toggling individual options and does not render an All option", async () => {
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
    expect(screen.queryByRole("option", { name: "All" })).not.toBeInTheDocument();

    await user.click(screen.getByRole("option", { name: "Heroism" }));
    expect(onChange).toHaveBeenCalledWith([]);
  });

  it("renders filter method selector and notifies on change", async () => {
    const user = userEvent.setup();
    const onMethodChange = vi.fn();
    render(
      <MultiSelect
        label="Traits"
        values={["Rebel", "Imperial"]}
        options={["Rebel", "Imperial", "Jedi"]}
        onChange={vi.fn()}
        method="or"
        onMethodChange={onMethodChange}
      />
    );

    await user.click(screen.getByRole("button", { name: "Traits (2)" }));

    const methodGroup = screen.getByRole("group", { name: "Traits Filter Method" });
    expect(methodGroup).toBeInTheDocument();

    const andButton = screen.getByRole("button", { name: "AND" });
    expect(andButton).toBeInTheDocument();
    await user.click(andButton);
    expect(onMethodChange).toHaveBeenCalledWith("and");

    const excludeButton = screen.getByRole("button", { name: "EXCLUDE" });
    await user.click(excludeButton);
    expect(onMethodChange).toHaveBeenCalledWith("exclude");
  });

  it("displays exclusion badges with NOT prefix when method is exclude", async () => {
    render(
      <MultiSelect
        label="Traits"
        values={["Imperial"]}
        options={["Imperial", "Rebel"]}
        onChange={vi.fn()}
        method="exclude"
        onMethodChange={vi.fn()}
      />
    );

    expect(screen.getByText("NOT Imperial")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Traits (Exclude: 1)" })).toBeInTheDocument();
  });
});
