import { cleanup, render, screen } from "@testing-library/react";
import { afterEach, describe, expect, it } from "vitest";
import { parseSwccgUniqueness, SwccgUniqueness } from "../SwccgUniqueness";

afterEach(cleanup);

describe("parseSwccgUniqueness", () => {
  it("parses single and multiple dots from asterisks and bullets", () => {
    expect(parseSwccgUniqueness("*")).toEqual({ type: "dot", count: 1 });
    expect(parseSwccgUniqueness("**")).toEqual({ type: "dot", count: 2 });
    expect(parseSwccgUniqueness("***")).toEqual({ type: "dot", count: 3 });
    expect(parseSwccgUniqueness("•")).toEqual({ type: "dot", count: 1 });
    expect(parseSwccgUniqueness("••")).toEqual({ type: "dot", count: 2 });
  });

  it("parses single and multiple diamonds from <> and unicode symbols", () => {
    expect(parseSwccgUniqueness("<>")).toEqual({ type: "diamond", count: 1 });
    expect(parseSwccgUniqueness("<><>")).toEqual({ type: "diamond", count: 2 });
    expect(parseSwccgUniqueness("<><><>")).toEqual({ type: "diamond", count: 3 });
    expect(parseSwccgUniqueness("◇◇◇")).toEqual({ type: "diamond", count: 3 });
  });

  it("returns null for empty or null inputs", () => {
    expect(parseSwccgUniqueness(null)).toBeNull();
    expect(parseSwccgUniqueness(undefined)).toBeNull();
    expect(parseSwccgUniqueness("")).toBeNull();
    expect(parseSwccgUniqueness("   ")).toBeNull();
  });
});

describe("SwccgUniqueness component", () => {
  it("renders dots for asterisks", () => {
    const { container } = render(<SwccgUniqueness uniqueness="**" side="Dark" />);
    const dots = screen.getByTestId("swccg-uniqueness-dots");
    expect(dots).toBeInTheDocument();
    expect(dots).toHaveTextContent("••");
    expect(dots).toHaveAttribute("aria-label", "2 dot uniqueness");
  });

  it("renders diamond SVGs and sr-only symbols for diamonds with dark side color", () => {
    const { container } = render(<SwccgUniqueness uniqueness="<><><>" side="Dark" />);
    const diamonds = screen.getByTestId("swccg-uniqueness-diamonds");
    expect(diamonds).toBeInTheDocument();
    expect(diamonds).toHaveAttribute("aria-label", "3 diamond uniqueness");
    expect(diamonds).toHaveTextContent("◇◇◇");

    const svgs = diamonds.querySelectorAll("svg");
    expect(svgs).toHaveLength(3);
    expect(svgs[0].className.baseVal).toContain("text-red-500");
  });

  it("renders diamonds with light side color", () => {
    render(<SwccgUniqueness uniqueness="<>" side="Light" />);
    const diamonds = screen.getByTestId("swccg-uniqueness-diamonds");
    const svgs = diamonds.querySelectorAll("svg");
    expect(svgs).toHaveLength(1);
    expect(svgs[0].className.baseVal).toContain("text-blue-400");
  });

  it("renders nothing when uniqueness is null or empty", () => {
    const { container } = render(<SwccgUniqueness uniqueness={null} />);
    expect(container.firstChild).toBeNull();
  });
});
