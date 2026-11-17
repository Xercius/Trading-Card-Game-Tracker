import { describe, expect, it } from "vitest";
import { parseChangelog } from "../parseChangelog";

const SAMPLE_CHANGELOG = `# Changelog

Introductory text.

## [v1.4.0] - 2026-11-17

### New Features
- Added What's New flyout.
* Added MTG deck validation.

### Improvements
- Improved importer speed.

### Bug Fixes
- Fixed proxy total bug.

## v1.3.0 - 2026-11-05

### New Features
- Added Magic importer.

## [v1.2.0] - 2026-10-20

### Bug Fixes
- Fixed filter reset.

## [v1.1.0] - 2026-10-01

### Improvements
- Improved grid rendering.

## [v1.0.0] - 2026-09-15

### New Features
- Initial release.

## [v0.9.0] - 2026-09-01

### Bug Fixes
- Beta fix.
`;

describe("parseChangelog", () => {
  it("parses version, date, and categorized changes up to the default limit of 5", () => {
    const releases = parseChangelog(SAMPLE_CHANGELOG);

    expect(releases).toHaveLength(5);
    expect(releases[0]).toEqual({
      version: "v1.4.0",
      date: "2026-11-17",
      sections: [
        {
          category: "New Features",
          items: ["Added What's New flyout.", "Added MTG deck validation."],
        },
        {
          category: "Improvements",
          items: ["Improved importer speed."],
        },
        {
          category: "Bug Fixes",
          items: ["Fixed proxy total bug."],
        },
      ],
    });
    expect(releases[1].version).toBe("v1.3.0");
    expect(releases[4].version).toBe("v1.0.0");
  });

  it("respects a custom maxReleases limit", () => {
    const releases = parseChangelog(SAMPLE_CHANGELOG, 2);
    expect(releases.map((r) => r.version)).toEqual(["v1.4.0", "v1.3.0"]);
  });

  it("returns an empty array for empty or non-matching markdown", () => {
    expect(parseChangelog("")).toEqual([]);
    expect(parseChangelog("# Nothing here")).toEqual([]);
    expect(parseChangelog(SAMPLE_CHANGELOG, 0)).toEqual([]);
  });

  it("ignores ## [Unreleased] sections before versioned releases", () => {
    const withUnreleased = `# Changelog

## [Unreleased]

### New Features
- Work in progress feature.

## [v0.26.1] - 2026-11-17

### New Features
- Shipped feature.
`;

    const releases = parseChangelog(withUnreleased);
    expect(releases).toEqual([
      {
        version: "v0.26.1",
        date: "2026-11-17",
        sections: [{ category: "New Features", items: ["Shipped feature."] }],
      },
    ]);
  });
});
