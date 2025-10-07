import { act } from "react-dom/test-utils";
import { createRoot } from "react-dom/client";
import { describe, expect, it } from "vitest";
import LineSparkline from "../LineSparkline";

describe("LineSparkline", () => {
  it("renders svg path for provided data", () => {
    const container = document.createElement("div");
    const root = createRoot(container);

    act(() => {
      root.render(
        <LineSparkline
          points={[
            { d: "2024-01-01", v: 1 },
            { d: "2024-01-02", v: 3 },
            { d: "2024-01-03", v: 2 },
          ]}
          ariaLabel="Sample sparkline"
        />
      );
    });

    const svg = container.querySelector("svg[aria-label='Sample sparkline']");
    expect(svg).not.toBeNull();
    const path = svg?.querySelector("path");
    expect(path?.getAttribute("d")).toMatch(/M0.00/);

    act(() => {
      root.unmount();
    });
  });
});
