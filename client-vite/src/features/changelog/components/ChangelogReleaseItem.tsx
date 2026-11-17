import { Badge } from "@/components/ui/badge";
import type { ChangelogCategory, ChangelogRelease } from "../types";

const CATEGORY_COLORS: Record<ChangelogCategory, string> = {
  "New Features": "text-emerald-400",
  Improvements: "text-sky-400",
  "Bug Fixes": "text-amber-400",
};

interface ChangelogReleaseItemProps {
  release: ChangelogRelease;
}

/**
 * Renders a single release entry with version badge, date, and categorized changes.
 */
export function ChangelogReleaseItem({ release }: ChangelogReleaseItemProps) {
  return (
    <article className="border-b border-border/60 pb-3.5 last:border-b-0 last:pb-0">
      <div className="mb-2 flex items-center justify-between gap-2">
        <Badge variant="secondary" className="font-mono text-xs">
          {release.version}
        </Badge>
        <time className="text-xs text-muted-foreground">{release.date}</time>
      </div>
      <div className="space-y-2">
        {release.sections.map((section) => (
          <div key={section.category}>
            <h4 className={`mb-1 text-xs font-semibold ${CATEGORY_COLORS[section.category]}`}>
              {section.category}
            </h4>
            <ul className="list-disc space-y-1 pl-4 text-xs text-foreground/90">
              {section.items.map((item, idx) => (
                <li key={idx}>{item}</li>
              ))}
            </ul>
          </div>
        ))}
      </div>
    </article>
  );
}
