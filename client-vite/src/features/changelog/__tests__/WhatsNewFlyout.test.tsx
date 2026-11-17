import { cleanup, fireEvent, render, screen } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { LS_CHANGELOG_LAST_VIEWED_KEY } from "@/constants";
import { WhatsNewFlyout } from "../components/WhatsNewFlyout";
import type { ChangelogRelease } from "../types";

const MOCK_RELEASES: ChangelogRelease[] = [
  {
    version: "v1.4.0",
    date: "2026-11-17",
    sections: [
      { category: "New Features", items: ["Added What's New flyout."] },
      { category: "Bug Fixes", items: ["Fixed query edge case."] },
    ],
  },
  {
    version: "v1.3.0",
    date: "2026-11-05",
    sections: [{ category: "Improvements", items: ["Faster sync workflow."] }],
  },
];

describe("WhatsNewFlyout", () => {
  beforeEach(() => {
    localStorage.clear();
  });

  afterEach(() => {
    cleanup();
    localStorage.clear();
    vi.restoreAllMocks();
  });

  it("shows unread indicator when localStorage has no viewed version", () => {
    render(<WhatsNewFlyout releases={MOCK_RELEASES} />);

    expect(screen.getByRole("button", { name: "What's new" })).toBeInTheDocument();
    expect(screen.getByText("Unread updates available")).toBeInTheDocument();
  });

  it("hides unread indicator when latest version was already viewed", () => {
    localStorage.setItem(LS_CHANGELOG_LAST_VIEWED_KEY, "v1.4.0");
    render(<WhatsNewFlyout releases={MOCK_RELEASES} />);

    expect(screen.queryByText("Unread updates available")).not.toBeInTheDocument();
  });

  it("opens the flyout, renders releases, marks as read, and persists to localStorage", () => {
    render(<WhatsNewFlyout releases={MOCK_RELEASES} />);

    const trigger = screen.getByRole("button", { name: "What's new" });
    fireEvent.click(trigger);

    const dialog = screen.getByRole("dialog", { name: "What's New" });
    expect(dialog).toBeInTheDocument();
    expect(trigger).toHaveAttribute("aria-expanded", "true");

    expect(screen.getByText("v1.4.0")).toBeInTheDocument();
    expect(screen.getByText("2026-11-17")).toBeInTheDocument();
    expect(screen.getByText("Added What's New flyout.")).toBeInTheDocument();
    expect(screen.getByText("Fixed query edge case.")).toBeInTheDocument();
    expect(screen.getByText("v1.3.0")).toBeInTheDocument();

    expect(localStorage.getItem(LS_CHANGELOG_LAST_VIEWED_KEY)).toBe("v1.4.0");
    expect(screen.queryByText("Unread updates available")).not.toBeInTheDocument();
  });

  it("closes on Escape and restores focus to trigger button", () => {
    render(<WhatsNewFlyout releases={MOCK_RELEASES} />);

    const trigger = screen.getByRole("button", { name: "What's new" });
    fireEvent.click(trigger);
    expect(screen.getByRole("dialog", { name: "What's New" })).toBeInTheDocument();

    fireEvent.keyDown(document, { key: "Escape" });

    expect(screen.queryByRole("dialog", { name: "What's New" })).not.toBeInTheDocument();
    expect(trigger).toHaveFocus();
  });

  it("closes when clicking outside the flyout", () => {
    render(
      <div>
        <WhatsNewFlyout releases={MOCK_RELEASES} />
        <button type="button">Outside</button>
      </div>
    );

    fireEvent.click(screen.getByRole("button", { name: "What's new" }));
    expect(screen.getByRole("dialog", { name: "What's New" })).toBeInTheDocument();

    fireEvent.mouseDown(screen.getByRole("button", { name: "Outside" }));
    expect(screen.queryByRole("dialog", { name: "What's New" })).not.toBeInTheDocument();
  });

  it("handles localStorage errors gracefully without throwing", () => {
    vi.spyOn(Storage.prototype, "getItem").mockImplementation(() => {
      throw new Error("Storage disabled");
    });
    vi.spyOn(Storage.prototype, "setItem").mockImplementation(() => {
      throw new Error("Quota exceeded");
    });

    render(<WhatsNewFlyout releases={MOCK_RELEASES} />);

    const trigger = screen.getByRole("button", { name: "What's new" });
    fireEvent.click(trigger);

    expect(screen.getByRole("dialog", { name: "What's New" })).toBeInTheDocument();
  });
});
