export type ImportSetOption = {
  code: string;
  name: string;
};

export type ImportSourceOption = {
  key: string;
  importerKey: string;
  displayName: string;
  games: string[];
  sets: ImportSetOption[];
};

export type ImportOptionsResponse = {
  sources: ImportSourceOption[];
};

export type ImportPreviewSummary = {
  new: number;
  update: number;
  duplicate: number;
  invalid: number;
};

export type ImportPreviewRow = {
  externalId: string;
  name: string;
  game: string;
  set: string;
  rarity: string | null;
  printingKey: string | null;
  imageUrl: string | null;
  price: number | null;
  status: string;
  messages: string[];
};

export type ImportPreviewResponse = {
  summary: ImportPreviewSummary;
  rows: ImportPreviewRow[];
};

export type ImportApplyResponse = {
  created: number;
  updated: number;
  skipped: number;
  invalid: number;
};

export type DryRunRemoteParams = {
  mode: "remote";
  source: string;
  set?: string;
};

export type DryRunUploadParams = {
  mode: "upload";
  source: string;
  file: File;
};

export type DryRunParams = DryRunRemoteParams | DryRunUploadParams;
