import type { ChangelogCategory, ChangelogRelease } from "./types";

export const LATEST_RELEASES_LIMIT = 5;

const RELEASE_HEADING_RE = /^##\s+\[?([^\]\s]+)\]?\s+-\s+(.+?)\s*$/;
const CATEGORY_HEADING_RE = /^###\s+(New Features|Improvements|Bug Fixes)\s*$/;
const LIST_ITEM_RE = /^[-*]\s+(.+?)\s*$/;

/**
 * Parses a Keep-a-Changelog-style markdown string into structured release entries,
 * capped at `maxReleases` so the in-app flyout stays concise.
 */
export function parseChangelog(
  markdown: string,
  maxReleases = LATEST_RELEASES_LIMIT
): ChangelogRelease[] {
  if (!markdown || maxReleases <= 0) {
    return [];
  }

  const releases: ChangelogRelease[] = [];
  let currentRelease: ChangelogRelease | null = null;
  let currentCategory: ChangelogCategory | null = null;

  for (const rawLine of markdown.split(/\r?\n/)) {
    const line = rawLine.trim();

    if (/^##\s+\[?Unreleased\]?/i.test(line)) {
      currentRelease = null;
      currentCategory = null;
      continue;
    }

    const releaseMatch = RELEASE_HEADING_RE.exec(line);
    if (releaseMatch) {
      if (releases.length >= maxReleases) {
        break;
      }
      currentRelease = {
        version: releaseMatch[1],
        date: releaseMatch[2],
        sections: [],
      };
      releases.push(currentRelease);
      currentCategory = null;
      continue;
    }

    if (!currentRelease) {
      continue;
    }

    const categoryMatch = CATEGORY_HEADING_RE.exec(line);
    if (categoryMatch) {
      currentCategory = categoryMatch[1] as ChangelogCategory;
      continue;
    }

    const itemMatch = LIST_ITEM_RE.exec(line);
    if (itemMatch && currentCategory) {
      let section = currentRelease.sections.find((s) => s.category === currentCategory);
      if (!section) {
        section = { category: currentCategory, items: [] };
        currentRelease.sections.push(section);
      }
      section.items.push(itemMatch[1]);
    }
  }

  return releases;
}
