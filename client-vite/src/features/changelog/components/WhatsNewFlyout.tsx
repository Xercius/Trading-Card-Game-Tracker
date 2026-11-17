import { useEffect, useRef, useState } from "react";
import { useChangelog } from "../useChangelog";
import type { ChangelogRelease } from "../types";
import { ChangelogReleaseItem } from "./ChangelogReleaseItem";

interface WhatsNewFlyoutProps {
  releases?: ChangelogRelease[];
}

function BellIcon() {
  return (
    <svg
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      strokeWidth="2"
      strokeLinecap="round"
      strokeLinejoin="round"
      aria-hidden="true"
      className="h-5 w-5"
    >
      <path d="M6 8a6 6 0 0 1 12 0c0 7 3 9 3 9H3s3-2 3-9" />
      <path d="M10.3 21a1.94 1.94 0 0 0 3.4 0" />
    </svg>
  );
}

/**
 * Header notification button and flyout panel displaying recent application updates.
 */
export function WhatsNewFlyout({ releases }: WhatsNewFlyoutProps) {
  const { releases: items, hasUnread, markAsRead } = useChangelog(releases);
  const [open, setOpen] = useState(false);
  const containerRef = useRef<HTMLDivElement>(null);
  const triggerRef = useRef<HTMLButtonElement>(null);

  useEffect(() => {
    if (!open) return;

    function handleClickOutside(event: MouseEvent) {
      if (containerRef.current && !containerRef.current.contains(event.target as Node)) {
        setOpen(false);
      }
    }

    function handleKeyDown(event: KeyboardEvent) {
      if (event.key === "Escape") {
        event.preventDefault();
        setOpen(false);
        triggerRef.current?.focus();
      }
    }

    document.addEventListener("mousedown", handleClickOutside);
    document.addEventListener("keydown", handleKeyDown);
    return () => {
      document.removeEventListener("mousedown", handleClickOutside);
      document.removeEventListener("keydown", handleKeyDown);
    };
  }, [open]);

  function handleToggle() {
    const next = !open;
    setOpen(next);
    if (next) {
      markAsRead();
    }
  }

  return (
    <div className="relative" ref={containerRef}>
      <button
        ref={triggerRef}
        type="button"
        aria-label="What's new"
        aria-expanded={open}
        aria-haspopup="dialog"
        aria-controls="whats-new-flyout"
        title="What's new"
        onClick={handleToggle}
        className="relative inline-flex items-center justify-center rounded-md border border-transparent p-2 text-foreground/80 transition-colors hover:border-border hover:bg-foreground/10 hover:text-foreground focus:outline-none focus-visible:ring-2 focus-visible:ring-primary"
      >
        <BellIcon />
        {hasUnread && (
          <>
            <span
              aria-hidden="true"
              className="absolute top-1.5 right-1.5 h-2 w-2 rounded-full bg-primary ring-2 ring-background"
            />
            <span className="sr-only">Unread updates available</span>
          </>
        )}
      </button>

      {open && (
        <div
          id="whats-new-flyout"
          role="dialog"
          aria-label="What's New"
          className="absolute right-0 mt-2 w-80 sm:w-96 max-h-[75vh] overflow-y-auto rounded-lg border border-border bg-popover p-4 text-popover-foreground shadow-xl z-50"
        >
          <div className="mb-3 flex items-center justify-between border-b border-border pb-2">
            <h3 className="text-sm font-semibold">What&apos;s New</h3>
            <span className="text-xs text-muted-foreground">Recent updates</span>
          </div>
          {items.length > 0 ? (
            <div className="space-y-3.5">
              {items.map((release) => (
                <ChangelogReleaseItem key={release.version} release={release} />
              ))}
            </div>
          ) : (
            <p className="text-xs text-muted-foreground">No release notes available yet.</p>
          )}
        </div>
      )}
    </div>
  );
}
