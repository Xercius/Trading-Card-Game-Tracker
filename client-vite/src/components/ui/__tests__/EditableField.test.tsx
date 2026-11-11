import { act } from "react";
import { createRoot } from "react-dom/client";
import { describe, expect, it, vi } from "vitest";
import { fireEvent } from "@testing-library/react";
import { EditableField } from "../EditableField";

describe("EditableField", () => {
  it("renders display mode with value and edit icon", async () => {
    const container = document.createElement("div");
    document.body.appendChild(container);
    const root = createRoot(container);

    await act(async () => {
      root.render(<EditableField value="My Deck" onSave={vi.fn()} ariaLabel="Deck Name" />);
    });

    expect(container.textContent).toContain("My Deck");
    const editBtn = container.querySelector("button[aria-label='Edit Deck Name']");
    expect(editBtn).not.toBeNull();

    await act(() => root.unmount());
    container.remove();
  });

  it("switches to edit mode on click and saves on Enter", async () => {
    const container = document.createElement("div");
    document.body.appendChild(container);
    const root = createRoot(container);
    const handleSave = vi.fn();

    await act(async () => {
      root.render(<EditableField value="My Deck" onSave={handleSave} ariaLabel="Deck Name" />);
    });

    const editBtn = container.querySelector(
      "button[aria-label='Edit Deck Name']"
    ) as HTMLButtonElement;
    await act(async () => {
      fireEvent.click(editBtn);
    });

    const input = container.querySelector("input") as HTMLInputElement;
    expect(input).not.toBeNull();
    expect(input.value).toBe("My Deck");

    await act(async () => {
      fireEvent.change(input, { target: { value: "Updated Deck Name" } });
      fireEvent.keyDown(input, { key: "Enter" });
    });

    expect(handleSave).toHaveBeenCalledWith("Updated Deck Name");

    await act(() => root.unmount());
    container.remove();
  });

  it("cancels edits and restores previous value on Escape", async () => {
    const container = document.createElement("div");
    document.body.appendChild(container);
    const root = createRoot(container);
    const handleSave = vi.fn();

    await act(async () => {
      root.render(
        <EditableField value="Original Name" onSave={handleSave} ariaLabel="Deck Name" />
      );
    });

    const trigger = container.querySelector("div[role='button']") as HTMLDivElement;
    await act(async () => {
      fireEvent.click(trigger);
    });

    const input = container.querySelector("input") as HTMLInputElement;
    await act(async () => {
      fireEvent.change(input, { target: { value: "Changed Name" } });
      fireEvent.keyDown(input, { key: "Escape" });
    });

    expect(handleSave).not.toHaveBeenCalled();
    expect(container.textContent).toContain("Original Name");

    await act(() => root.unmount());
    container.remove();
  });

  it("saves on blur when focus leaves input", async () => {
    const container = document.createElement("div");
    document.body.appendChild(container);
    const root = createRoot(container);
    const handleSave = vi.fn();

    await act(async () => {
      root.render(
        <EditableField
          value="Deck Description"
          onSave={handleSave}
          multiline
          ariaLabel="Deck Description"
        />
      );
    });

    const trigger = container.querySelector("div[role='button']") as HTMLDivElement;
    await act(async () => {
      fireEvent.click(trigger);
    });

    const textarea = container.querySelector("textarea") as HTMLTextAreaElement;
    expect(textarea).not.toBeNull();

    await act(async () => {
      fireEvent.change(textarea, { target: { value: "New Description" } });
      fireEvent.blur(textarea);
    });

    expect(handleSave).toHaveBeenCalledWith("New Description");

    await act(() => root.unmount());
    container.remove();
  });
});
