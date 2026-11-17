import { cleanup, fireEvent, render, screen } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";
import { MtgColorFilter } from "../MtgColorFilter";
import { MtgColorIdentityFilter } from "../MtgColorIdentityFilter";

describe("MtgColorFilter", () => {
  afterEach(() => {
    cleanup();
  });

  it("renders checkboxes for colors and OR / AND / EXACT operators", () => {
    const onChange = vi.fn();
    render(<MtgColorFilter values={["White"]} method="or" onChange={onChange} />);

    fireEvent.click(screen.getByRole("button", { name: /Colors \(White\)/ }));

    expect(screen.getByRole("button", { name: "OR" })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "AND" })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "EXACT" })).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Exclude" })).not.toBeInTheDocument();

    const whiteCheckbox = screen.getByRole("checkbox", { name: "White" });
    const blueCheckbox = screen.getByRole("checkbox", { name: "Blue" });
    expect(whiteCheckbox).toBeChecked();
    expect(blueCheckbox).not.toBeChecked();

    fireEvent.click(blueCheckbox);
    expect(onChange).toHaveBeenCalledWith(["White", "Blue"], "or");

    fireEvent.click(screen.getByRole("button", { name: "AND" }));
    expect(onChange).toHaveBeenCalledWith(["White"], "subset");

    fireEvent.click(screen.getByRole("button", { name: "EXACT" }));
    expect(onChange).toHaveBeenCalledWith(["White"], "exact");
  });
});

describe("MtgColorIdentityFilter", () => {
  afterEach(() => {
    cleanup();
  });

  it("renders checkboxes without an operator selector", () => {
    const onChange = vi.fn();
    render(<MtgColorIdentityFilter values={[]} onChange={onChange} />);

    fireEvent.click(screen.getByRole("button", { name: "Color Identity" }));

    expect(screen.queryByText("Filter Method")).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "OR" })).not.toBeInTheDocument();

    fireEvent.click(screen.getByRole("checkbox", { name: "Colorless" }));
    expect(onChange).toHaveBeenCalledWith(["Colorless"]);
  });
});
