import { render, screen, cleanup } from "@testing-library/react";
import { describe, it, expect, afterEach } from "vitest";
import { LightsaberBlade, ForceIconBadge } from "../SwccgForceIcons";

describe("SwccgForceIcons", () => {
  afterEach(cleanup);

  it("renders a dark side lightsaber blade with red glow", () => {
    const { container } = render(<LightsaberBlade side="dark" />);
    const svg = container.querySelector("svg");
    expect(svg).toBeInTheDocument();
    expect(svg).toHaveAttribute("aria-hidden", "true");
  });

  it("renders a light side lightsaber blade with blue glow", () => {
    const { container } = render(<LightsaberBlade side="light" />);
    const svg = container.querySelector("svg");
    expect(svg).toBeInTheDocument();
  });

  it("renders a dark side badge with count and blade icons", () => {
    render(<ForceIconBadge side="dark" count={2} />);
    const badge = screen.getByLabelText("Dark Side Icons: 2");
    expect(badge).toBeInTheDocument();
    expect(screen.getByText("Dark Icons")).toBeInTheDocument();
    expect(screen.getByText("2")).toBeInTheDocument();
  });

  it("renders a light side badge with count and blade icons", () => {
    render(<ForceIconBadge side="light" count={1} />);
    const badge = screen.getByLabelText("Light Side Icons: 1");
    expect(badge).toBeInTheDocument();
    expect(screen.getByText("Light Icons")).toBeInTheDocument();
    expect(screen.getByText("1")).toBeInTheDocument();
  });

  it("renders zero count properly without crashing", () => {
    render(<ForceIconBadge side="dark" count={0} />);
    expect(screen.getByLabelText("Dark Side Icons: 0")).toBeInTheDocument();
    expect(screen.getByText("0")).toBeInTheDocument();
  });
});
