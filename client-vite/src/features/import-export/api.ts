import { useMutation, useQueryClient } from "@tanstack/react-query";
import http from "@/lib/http";
import { toProblemDetailsError } from "@/lib/problemDetails";
import { queryKeys } from "@/lib/queryKeys";
import { collectionKeys } from "@/features/collection/api";
import { deckBuilderKeys } from "@/features/decks/api";

export type ImportMode = "merge" | "replace";

export interface ExportCollectionItem {
  cardPrintingId: number;
  qtyOwned: number;
  qtyProxyOwned: number;
  game?: string | null;
  cardName?: string | null;
  setCode?: string | null;
  setName?: string | null;
  number?: string | null;
  style?: string | null;
}

export interface ExportWishlistSourceItem {
  deckGame?: string | null;
  deckName?: string | null;
  qty: number;
}

export interface ExportWishlistItem {
  cardPrintingId: number;
  qty: number;
  game?: string | null;
  cardName?: string | null;
  setCode?: string | null;
  setName?: string | null;
  number?: string | null;
  style?: string | null;
  sources?: ExportWishlistSourceItem[] | null;
}

export interface ExportDeckCard {
  cardPrintingId: number;
  inDeck: number;
  idea: number;
  acquire: number;
  proxy: number;
  sideboard?: number;
  isStarting?: boolean;
  isCommander?: boolean;
  cardName?: string | null;
  setCode?: string | null;
  setName?: string | null;
  number?: string | null;
  style?: string | null;
}

export interface ExportDeck {
  game: string;
  name: string;
  description?: string | null;
  format?: string | null;
  side?: string | null;
  cards: ExportDeckCard[];
}

export interface ExportPayload {
  version: number;
  collection: ExportCollectionItem[];
  wishlist: ExportWishlistItem[];
  decks: ExportDeck[];
}

export interface ImportUnresolvedItem {
  section: string;
  cardPrintingId: number;
  game?: string | null;
  cardName?: string | null;
  setCode?: string | null;
  number?: string | null;
  reason: string;
}

export interface ImportSummaryCounts {
  collection: number;
  wishlist: number;
  decks: number;
  deckCards: number;
  resolvedByNaturalKey: number;
  unresolvedCount: number;
}

export interface ImportJsonResponse {
  imported: ImportSummaryCounts;
  unresolved?: ImportUnresolvedItem[] | null;
}

function parseDownloadFilename(contentDisposition?: string): string {
  if (contentDisposition) {
    const match = /filename="?([^";]+)"?/i.exec(contentDisposition);
    if (match?.[1]) return match[1];
  }
  return "tcgtracker_backup.json";
}

/**
 * Downloads the user's collection, wishlist, and decks backup as a portable JSON file.
 */
export async function downloadJsonBackup(): Promise<string> {
  try {
    const response = await http.get<Blob>("export/json", { responseType: "blob" });
    const fileName = parseDownloadFilename(
      response.headers?.["content-disposition"] as string | undefined
    );
    const url = URL.createObjectURL(response.data);
    try {
      const link = document.createElement("a");
      link.href = url;
      link.download = fileName;
      document.body.appendChild(link);
      link.click();
      link.remove();
    } finally {
      URL.revokeObjectURL(url);
    }
    return fileName;
  } catch (error) {
    throw toProblemDetailsError(error);
  }
}

/**
 * Restores a JSON backup in merge or replace mode.
 */
export async function importJsonBackup(
  payload: ExportPayload,
  mode: ImportMode
): Promise<ImportJsonResponse> {
  try {
    const response = await http.post<ImportJsonResponse>("import/json", payload, {
      params: { mode },
    });
    return response.data;
  } catch (error) {
    throw toProblemDetailsError(error);
  }
}

/**
 * React Query mutation hook to import a JSON backup and refresh affected caches.
 */
export function useImportJsonBackupMutation() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ payload, mode }: { payload: ExportPayload; mode: ImportMode }) =>
      importJsonBackup(payload, mode),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: collectionKeys.all });
      queryClient.invalidateQueries({ queryKey: queryKeys.wishlist.all() });
      queryClient.invalidateQueries({ queryKey: queryKeys.decks.all() });
      queryClient.invalidateQueries({ queryKey: deckBuilderKeys.all });
      queryClient.invalidateQueries({ queryKey: ["value"] });
      queryClient.invalidateQueries({ queryKey: queryKeys.importExport.all() });
    },
  });
}
