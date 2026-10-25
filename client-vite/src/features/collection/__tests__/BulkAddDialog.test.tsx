import { act } from "react-dom/test-utils";
import { createRoot } from "react-dom/client";
import { describe, expect, it, vi, beforeEach } from "vitest";
import { fireEvent } from "@testing-library/react";
import BulkAddDialog from "../BulkAddDialog";

const mutateAsync = vi.fn();

vi.mock("../api", () => ({
  useBulkUpdateMutation: () => ({ mutateAsync }),
}));

describe("BulkAddDialog", () => {
  beforeEach(() => {
    mutateAsync.mockReset();
    mutateAsync.mockResolvedValue(undefined);
  });

  it("parses CSV and calls mutation with expected payload", async () => {
    const container = document.createElement("div");
    document.body.appendChild(container);
    const root = createRoot(container);

    act(() => {
      root.render(<BulkAddDialog queryKey={["collection"]} />);
    });

    const trigger = container.querySelector<HTMLButtonElement>("button");
    expect(trigger).not.toBeNull();

    await act(async () => {
      trigger?.click();
    });

    const textarea = document.body.querySelector<HTMLTextAreaElement>("textarea");
    expect(textarea).not.toBeNull();
    const numberInputs = document.body.querySelectorAll<HTMLInputElement>("input[type='number']");
    expect(numberInputs.length).toBeGreaterThanOrEqual(2);

    await act(async () => {
      if (textarea) fireEvent.change(textarea, { target: { value: "1001,2,0\n1002" } });
      fireEvent.change(numberInputs[0], { target: { value: "3" } });
      fireEvent.change(numberInputs[1], { target: { value: "1" } });
    });

    const apply = Array.from(document.body.querySelectorAll<HTMLButtonElement>("button")).find(
      (btn) => btn.textContent === "Apply changes"
    );
    expect(apply).not.toBeNull();

    await act(async () => {
      apply?.click();
      await Promise.resolve();
    });

    expect(mutateAsync).toHaveBeenCalledTimes(1);
    expect(mutateAsync).toHaveBeenCalledWith({
      items: [
        { printingId: 1001, ownedDelta: 2, proxyDelta: 0 },
        { printingId: 1002, ownedDelta: 3, proxyDelta: 1 },
      ],
    });

    await act(() => {
      root.unmount();
    });
    container.remove();
  });
});
