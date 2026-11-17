export type ChangelogCategory = "New Features" | "Improvements" | "Bug Fixes";

export interface ChangelogSection {
  category: ChangelogCategory;
  items: string[];
}

export interface ChangelogRelease {
  version: string;
  date: string;
  sections: ChangelogSection[];
}
