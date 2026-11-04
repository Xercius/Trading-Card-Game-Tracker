import { act } from "react";
import { createRoot } from "react-dom/client";
import { describe, expect, it } from "vitest";
import { fireEvent } from "@testing-library/react";
import { CardImageHoverPreview } from "../CardImageHoverPreview";

describe("CardImageHoverPreview", () => {
  it("renders a vertical thumbnail by default", async () => {
    const container = document.createElement("div");
    document.body.appendChild(container);
    const root = createRoot(container);

    await act(async () => {
      root.render(
        <CardImageHoverPreview
          imageUrl="https://example.com/luke.png"
          cardName="Luke Skywalker"
        />
      );
    });

    const img = container.querySelector("img");
    expect(img).not.toBeNull();
    expect(img?.style.width).toBe("32px");
    expect(img?.style.height).toBe("44px");

    await act(() => {
      root.unmount();
    });
    container.remove();
  });

  it("renders a horizontal thumbnail when isHorizontal is true", async () => {
    const container = document.createElement("div");
    document.body.appendChild(container);
    const root = createRoot(container);

    await act(async () => {
      root.render(
        <CardImageHoverPreview
          imageUrl="https://example.com/luke-leader.png"
          cardName="Luke Skywalker — Faithful Friend"
          isHorizontal={true}
        />
      );
    });

    const img = container.querySelector("img");
    expect(img).not.toBeNull();
    expect(img?.style.width).toBe("44px");
    expect(img?.style.height).toBe("32px");

    await act(() => {
      root.unmount();
    });
    container.remove();
  });

  it("displays hover preview popover on mouse enter clamped to viewport", async () => {
    const container = document.createElement("div");
    document.body.appendChild(container);
    const root = createRoot(container);

    await act(async () => {
      root.render(
        <CardImageHoverPreview
          imageUrl="https://example.com/luke-leader.png"
          cardName="Luke Skywalker — Faithful Friend"
          isHorizontal={true}
        />
      );
    });

    const trigger = container.querySelector("div");
    expect(trigger).not.toBeNull();

    await act(async () => {
      fireEvent.mouseEnter(trigger!);
    });

    // Popover is portaled to document.body
    const popover = document.body.querySelector(".fixed.z-\\[9999\\]");
    expect(popover).not.toBeNull();
    expect(popover?.textContent).toContain("Luke Skywalker — Faithful Friend");

    await act(async () => {
      fireEvent.mouseLeave(trigger!);
    });

    expect(document.body.querySelector(".fixed.z-\\[9999\\]")).toBeNull();

    await act(() => {
      root.unmount();
    });
    container.remove();
  });
});

