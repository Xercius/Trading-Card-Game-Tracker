import rawChangelog from "../../../../CHANGELOG.md?raw";
import { LATEST_RELEASES_LIMIT, parseChangelog } from "./parseChangelog";
import type { ChangelogRelease } from "./types";

/**
 * Latest application releases parsed at build time from the root `CHANGELOG.md`.
 */
export const CHANGELOG_RELEASES: ChangelogRelease[] = parseChangelog(
  rawChangelog,
  LATEST_RELEASES_LIMIT
);
