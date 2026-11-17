import { execFileSync } from "node:child_process";
import { existsSync, readFileSync, writeFileSync } from "node:fs";
import { resolve } from "node:path";
import { pathToFileURL } from "node:url";

const CATEGORY_ORDER = ["New Features", "Improvements", "Bug Fixes"];
const NOISE_COMMIT_RE =
  /^(chore\(release\):|build\(deps(-dev)?\):|merge branch |merge pull request )/i;

/**
 * Maps a raw git commit subject line into a changelog category and cleaned bullet text.
 * Returns null for automated release/dependency noise commits.
 */
export function categorizeCommitSubject(rawSubject) {
  const subject = (rawSubject ?? "").trim();
  if (!subject || NOISE_COMMIT_RE.test(subject)) {
    return null;
  }

  const conventionalMatch = /^([a-z]+)(?:\([^)]+\))?!?:\s*(.+)$/i.exec(subject);
  if (conventionalMatch) {
    const type = conventionalMatch[1].toLowerCase();
    const description = capitalizeFirst(conventionalMatch[2].trim());
    if (type === "feat") {
      return { category: "New Features", text: ensurePeriod(description) };
    }
    if (type === "fix") {
      return { category: "Bug Fixes", text: ensurePeriod(description) };
    }
    if (type === "chore" || type === "ci" || type === "test" || type === "style") {
      return null;
    }
    return { category: "Improvements", text: ensurePeriod(description) };
  }

  if (/^(\[feature\]|add\b|implement\b|introduce\b)/i.test(subject)) {
    return {
      category: "New Features",
      text: ensurePeriod(capitalizeFirst(subject.replace(/^\[feature\]:?\s*/i, ""))),
    };
  }

  if (/^(\[bug\]|fix\b|resolve\b|hotfix\b)/i.test(subject)) {
    return {
      category: "Bug Fixes",
      text: ensurePeriod(capitalizeFirst(subject.replace(/^\[bug\]:?\s*/i, ""))),
    };
  }

  return {
    category: "Improvements",
    text: ensurePeriod(capitalizeFirst(subject.replace(/^\[task\]:?\s*/i, ""))),
  };
}

function capitalizeFirst(str) {
  if (!str) return str;
  return str.charAt(0).toUpperCase() + str.slice(1);
}

function ensurePeriod(str) {
  if (!str) return str;
  return /[.!?]$/.test(str) ? str : `${str}.`;
}

function parseSemver(version) {
  const clean = version.replace(/^v/i, "").trim();
  const parts = clean.split(".").map((n) => Number.parseInt(n, 10) || 0);
  return [parts[0] ?? 0, parts[1] ?? 0, parts[2] ?? 0];
}

function compareSemver(a, b) {
  const pa = parseSemver(a);
  const pb = parseSemver(b);
  for (let i = 0; i < 3; i++) {
    if (pa[i] !== pb[i]) return pa[i] - pb[i];
  }
  return 0;
}

/**
 * Formats a versioned release markdown section from categorized bullet lists.
 */
export function formatReleaseSection(version, date, sectionsByCategory) {
  const normalizedVersion = version.startsWith("v") ? version : `v${version}`;
  const lines = [`## [${normalizedVersion}] - ${date}`];

  for (const category of CATEGORY_ORDER) {
    const items = sectionsByCategory[category] ?? [];
    if (items.length === 0) continue;
    lines.push("", `### ${category}`, "");
    for (const item of items) {
      lines.push(`- ${item}`);
    }
  }

  return lines.join("\n");
}

/**
 * Extracts any categorized items currently listed under `## [Unreleased]`.
 */
export function extractUnreleasedSections(markdown) {
  const lines = markdown.split(/\r?\n/);
  let inUnreleased = false;
  let currentCategory = null;
  const sections = {
    "New Features": [],
    Improvements: [],
    "Bug Fixes": [],
  };

  for (const rawLine of lines) {
    const line = rawLine.trim();
    if (/^##\s+\[?Unreleased\]?/i.test(line)) {
      inUnreleased = true;
      currentCategory = null;
      continue;
    }
    if (inUnreleased && /^##\s+/.test(line)) {
      break;
    }
    if (!inUnreleased) continue;

    const categoryMatch = /^###\s+(New Features|Improvements|Bug Fixes)\s*$/.exec(line);
    if (categoryMatch) {
      currentCategory = categoryMatch[1];
      continue;
    }

    const itemMatch = /^[-*]\s+(.+?)\s*$/.exec(line);
    if (itemMatch && currentCategory) {
      sections[currentCategory].push(itemMatch[1]);
    }
  }

  const hasItems = Object.values(sections).some((items) => items.length > 0);
  return { hasItems, sections };
}

/**
 * Updates `CHANGELOG.md` content for a new release by promoting `## [Unreleased]` items
 * or falling back to categorized commit subjects when `## [Unreleased]` is empty.
 */
export function applyReleaseToChangelog({
  markdown,
  version,
  date,
  commitSubjects = [],
}) {
  const normalizedVersion = version.startsWith("v") ? version : `v${version}`;
  const escapedVersion = normalizedVersion.replace(/[.*+?^${}()|[\]\\]/g, "\\$&");
  if (new RegExp(`^##\\s+\\[?${escapedVersion}\\]?\\b`, "m").test(markdown)) {
    return { updated: false, markdown };
  }

  const { hasItems, sections: unreleasedSections } = extractUnreleasedSections(markdown);
  const targetSections = {
    "New Features": [],
    Improvements: [],
    "Bug Fixes": [],
  };

  if (hasItems) {
    for (const category of CATEGORY_ORDER) {
      targetSections[category].push(...unreleasedSections[category]);
    }
  } else {
    const seen = new Set();
    for (const subject of commitSubjects) {
      const categorized = categorizeCommitSubject(subject);
      if (!categorized) continue;
      const key = `${categorized.category}:${categorized.text.toLowerCase()}`;
      if (seen.has(key)) continue;
      seen.add(key);
      targetSections[categorized.category].push(categorized.text);
    }
  }

  const totalItems = Object.values(targetSections).reduce((sum, list) => sum + list.length, 0);
  if (totalItems === 0) {
    targetSections.Improvements.push(`Maintenance and stability updates for ${normalizedVersion}.`);
  }

  const releaseBlock = formatReleaseSection(normalizedVersion, date, targetSections);

  const withoutUnreleasedBody = markdown.replace(
    /##\s+\[?Unreleased\]?[\s\S]*?(?=^##\s+\[?v?\d|\Z)/m,
    "## [Unreleased]\n\n"
  );

  // Find the first existing release section that is older than `normalizedVersion`
  const releaseRegex = /^##\s+\[?(v?\d+\.\d+\.\d+)\]?\s+-\s+.+$/gm;
  let match;
  while ((match = releaseRegex.exec(withoutUnreleasedBody)) !== null) {
    if (compareSemver(normalizedVersion, match[1]) > 0) {
      const idx = match.index;
      const nextMarkdown =
        withoutUnreleasedBody.slice(0, idx) +
        `${releaseBlock}\n\n` +
        withoutUnreleasedBody.slice(idx);
      return { updated: true, markdown: nextMarkdown };
    }
  }

  return {
    updated: true,
    markdown: `${withoutUnreleasedBody.trimEnd()}\n\n${releaseBlock}\n`,
  };
}

function runGit(args) {
  return execFileSync("git", args, { encoding: "utf8" }).trim();
}

function getCommitSubjects(fromRef, toRef = "HEAD") {
  const range = fromRef ? `${fromRef}..${toRef}` : toRef;
  const output = runGit(["log", range, "--no-merges", "--format=%s"]);
  return output
    .split(/\r?\n/)
    .map((line) => line.trim())
    .filter(Boolean);
}

function parseCliArgs(argv) {
  const opts = {
    version: null,
    from: null,
    date: new Date().toISOString().slice(0, 10),
    dryRun: false,
    syncTags: false,
    limit: 5,
  };

  for (let i = 0; i < argv.length; i++) {
    const arg = argv[i];
    if (arg === "--version" && argv[i + 1]) opts.version = argv[++i];
    else if (arg === "--from" && argv[i + 1]) opts.from = argv[++i];
    else if (arg === "--date" && argv[i + 1]) opts.date = argv[++i];
    else if (arg === "--limit" && argv[i + 1]) opts.limit = Number(argv[++i]) || 5;
    else if (arg === "--dry-run") opts.dryRun = true;
    else if (arg === "--sync-tags") opts.syncTags = true;
  }

  return opts;
}

function main() {
  const opts = parseCliArgs(process.argv.slice(2));
  const changelogPath = resolve(process.cwd(), existsSync("CHANGELOG.md") ? "CHANGELOG.md" : "../CHANGELOG.md");
  let content = readFileSync(changelogPath, "utf8");

  if (opts.syncTags) {
    const tags = runGit(["tag", "--sort=-creatordate"])
      .split(/\r?\n/)
      .map((t) => t.trim())
      .filter(Boolean)
      .slice(0, opts.limit);

    let changed = false;
    for (let i = tags.length - 1; i >= 0; i--) {
      const tag = tags[i];
      const prevTag = tags[i + 1] ?? null;
      const tagDate = runGit(["log", "-1", "--format=%ad", "--date=short", tag]);
      const subjects = prevTag ? getCommitSubjects(prevTag, tag) : getCommitSubjects(null, `${tag}~1..${tag}`);
      const res = applyReleaseToChangelog({
        markdown: content,
        version: tag,
        date: tagDate || opts.date,
        commitSubjects: subjects,
      });
      if (res.updated) {
        content = res.markdown;
        changed = true;
      }
    }

    if (opts.dryRun) {
      process.stdout.write(content);
      return;
    }
    if (changed) {
      writeFileSync(changelogPath, content, "utf8");
    }
    return;
  }

  if (!opts.version) {
    throw new Error("Missing required --version <vX.Y.Z> or --sync-tags flag.");
  }

  const subjects = getCommitSubjects(opts.from, "HEAD");
  const result = applyReleaseToChangelog({
    markdown: content,
    version: opts.version,
    date: opts.date,
    commitSubjects: subjects,
  });

  if (opts.dryRun) {
    process.stdout.write(result.markdown);
    return;
  }

  if (result.updated) {
    writeFileSync(changelogPath, result.markdown, "utf8");
  }
}

if (process.argv[1] && import.meta.url === pathToFileURL(resolve(process.argv[1])).href) {
  main();
}
