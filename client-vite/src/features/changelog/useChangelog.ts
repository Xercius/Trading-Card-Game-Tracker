import { useCallback, useState } from "react";
import { LS_CHANGELOG_LAST_VIEWED_KEY } from "@/constants";
import { CHANGELOG_RELEASES } from "./changelogData";
import type { ChangelogRelease } from "./types";

function readLastViewedVersion(): string | null {
  if (typeof window === "undefined") return null;
  try {
    return window.localStorage.getItem(LS_CHANGELOG_LAST_VIEWED_KEY);
  } catch {
    return null;
  }
}

function writeLastViewedVersion(version: string): void {
  if (typeof window === "undefined") return;
  try {
    window.localStorage.setItem(LS_CHANGELOG_LAST_VIEWED_KEY, version);
  } catch {
    // Ignore storage quota or privacy mode errors
  }
}

/**
 * Tracks whether the user has viewed the latest changelog release using `localStorage`.
 */
export function useChangelog(releases: ChangelogRelease[] = CHANGELOG_RELEASES) {
  const [lastViewedVersion, setLastViewedVersion] = useState<string | null>(() =>
    readLastViewedVersion()
  );

  const latestVersion = releases[0]?.version ?? null;
  const hasUnread = latestVersion !== null && lastViewedVersion !== latestVersion;

  const markAsRead = useCallback(() => {
    if (!latestVersion) return;
    setLastViewedVersion(latestVersion);
    writeLastViewedVersion(latestVersion);
  }, [latestVersion]);

  return {
    releases,
    hasUnread,
    markAsRead,
  };
}
