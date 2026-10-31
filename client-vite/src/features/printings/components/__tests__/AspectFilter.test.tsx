import { describe, expect, it, vi, afterEach } from "vitest";
import { render, screen, fireEvent, cleanup } from "@testing-library/react";
import { AspectFilter } from "../AspectFilter";

describe("AspectFilter", () => {
  afterEach(() => {
    cleanup();
  });

  it("renders with default summary when no aspects are selected", () => {
    render(<AspectFilter values={[]} method="or" onChange={vi.fn()} />);

    expect(screen.getByRole("button", { name: "Aspects" })).toBeInTheDocument();
  });

  it("opens popup and renders method selector and aspects", () => {
    render(<AspectFilter values={[]} method="or" onChange={vi.fn()} />);

    fireEvent.click(screen.getByRole("button", { name: "Aspects" }));

    expect(screen.getByRole("dialog", { name: /Filter by Aspect/i })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "OR" })).toHaveAttribute("aria-pressed", "true");
    expect(screen.getByRole("button", { name: "AND" })).toHaveAttribute("aria-pressed", "false");
    expect(screen.getByRole("button", { name: "EXCLUDE" })).toHaveAttribute(
      "aria-pressed",
      "false"
    );

    expect(screen.getByText("Aggression")).toBeInTheDocument();
    expect(screen.getByText("Command")).toBeInTheDocument();
    expect(screen.getByText("Vigilance")).toBeInTheDocument();
    expect(screen.getByText("Cunning")).toBeInTheDocument();
    expect(screen.getByText("Heroism")).toBeInTheDocument();
    expect(screen.getByText("Villainy")).toBeInTheDocument();
    expect(screen.getByLabelText("No Aspect (Neutral)")).not.toBeChecked();
  });

  it("increments and decrements aspect counts", () => {
    const onChange = vi.fn();
    render(<AspectFilter values={["Aggression"]} method="or" onChange={onChange} />);

    fireEvent.click(screen.getByRole("button", { name: /Aspects \(Aggression\)/i }));

    const increaseBtn = screen.getByRole("button", { name: "Increase Aggression" });
    fireEvent.click(increaseBtn);

    expect(onChange).toHaveBeenCalledWith(["Aggression", "Aggression"], "or");

    const decreaseBtn = screen.getByRole("button", { name: "Decrease Aggression" });
    fireEvent.click(decreaseBtn);

    expect(onChange).toHaveBeenCalledWith([], "or");
  });

  it("switches filter method to EXCLUDE", () => {
    const onChange = vi.fn();
    render(<AspectFilter values={["Aggression"]} method="or" onChange={onChange} />);

    fireEvent.click(screen.getByRole("button", { name: /Aspects \(Aggression\)/i }));

    const excludeBtn = screen.getByRole("button", { name: "EXCLUDE" });
    fireEvent.click(excludeBtn);

    expect(onChange).toHaveBeenCalledWith(["Aggression"], "exclude");
  });

  it("toggles No Aspect option", () => {
    const onChange = vi.fn();
    render(<AspectFilter values={[]} method="or" onChange={onChange} />);

    fireEvent.click(screen.getByRole("button", { name: "Aspects" }));

    const checkbox = screen.getByLabelText("No Aspect (Neutral)");
    fireEvent.click(checkbox);

    expect(onChange).toHaveBeenCalledWith(["no-aspect"], "or");
  });

  it("displays summary with multiplicity and exclude prefix", () => {
    render(
      <AspectFilter
        values={["Aggression", "Aggression", "Command"]}
        method="exclude"
        onChange={vi.fn()}
      />
    );

    expect(
      screen.getByRole("button", { name: /Aspects \(Exclude: Command, Aggression ×2\)/i })
    ).toBeInTheDocument();
  });

  it("resets all aspects and method when clicking Reset", () => {
    const onChange = vi.fn();
    render(
      <AspectFilter values={["Aggression", "Aggression"]} method="exclude" onChange={onChange} />
    );

    fireEvent.click(screen.getByRole("button", { name: /Aspects/i }));

    const resetBtn = screen.getByRole("button", { name: "Reset" });
    fireEvent.click(resetBtn);

    expect(onChange).toHaveBeenCalledWith([], "or");
  });
});
