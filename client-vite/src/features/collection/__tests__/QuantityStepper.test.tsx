import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { act, fireEvent, render, screen } from "@testing-library/react";
import { QuantityStepper } from "../components/QuantityStepper";

describe("QuantityStepper", () => {
  beforeEach(() => {
    vi.useFakeTimers();
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it("updates immediately on rapid clicks and coalesces into a single commit", () => {
    const onCommit = vi.fn();
    render(
      <QuantityStepper
        value={0}
        debounceMs={250}
        ariaLabel="Luke Skywalker owned quantity"
        onCommit={onCommit}
      />
    );

    const incBtn = screen.getByRole("button", { name: "Increase Luke Skywalker owned quantity" });
    const input = screen.getByRole("spinbutton", { name: "Luke Skywalker owned quantity" });

    fireEvent.click(incBtn);
    fireEvent.click(incBtn);
    fireEvent.click(incBtn);
    fireEvent.click(incBtn);

    expect(input).toHaveValue(4);
    expect(incBtn).not.toBeDisabled();
    expect(onCommit).not.toHaveBeenCalled();

    act(() => {
      vi.advanceTimersByTime(250);
    });

    expect(onCommit).toHaveBeenCalledTimes(1);
    expect(onCommit).toHaveBeenCalledWith(4);
  });

  it("commits multi-digit typed values on blur without firing per keystroke", () => {
    const onCommit = vi.fn();
    render(<QuantityStepper value={1} ariaLabel="Sol Ring owned quantity" onCommit={onCommit} />);

    const input = screen.getByRole("spinbutton", { name: "Sol Ring owned quantity" });
    fireEvent.focus(input);
    fireEvent.change(input, { target: { value: "12" } });

    expect(onCommit).not.toHaveBeenCalled();

    fireEvent.blur(input);

    expect(onCommit).toHaveBeenCalledTimes(1);
    expect(onCommit).toHaveBeenCalledWith(12);
  });
});
