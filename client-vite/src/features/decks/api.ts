import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { api } from "@/lib/api";
import { collectionKeys } from "@/features/collection/api";
import { QUERY_STALE_MS_VALUE_HISTORY } from "@/constants";
import type { ValuePoint } from "@/types/value";
import { toProblemDetailsError } from "@/lib/problemDetails";

export type DeckFeaturedCard = {
  cardPrintingId: number;
  cardName: string;
  imageUrl: string | null;
  cardType: string;
  isHorizontal: boolean;
};

export type DeckDetails = {
  id: number;
  userId: number;
  game: string;
  name: string;
  description: string | null;
  createdUtc: string;
  updatedUtc: string | null;
  cardCount?: number;
  featuredCards?: DeckFeaturedCard[];
  format?: string | null;
  side?: string | null;
  aspects?: string[] | null;
  baseCard?: DeckFeaturedCard | null;
  defensiveShieldCount?: number;
  mainDeckCardCount?: number;
};

export type DeckListItem = {
  id: number;
  userId: number;
  game: string;
  name: string;
  description: string | null;
  createdUtc: string;
  updatedUtc: string | null;
  cardCount: number;
  featuredCards?: DeckFeaturedCard[];
  format?: string | null;
  side?: string | null;
  aspects?: string[] | null;
  baseCard?: DeckFeaturedCard | null;
  defensiveShieldCount?: number;
  mainDeckCardCount?: number;
};

export type PagedDecks = {
  items: DeckListItem[];
  total: number;
  page: number;
  pageSize: number;
};

export type DeckCardWithAvailability = {
  printingId: number;
  cardName: string;
  imageUrl: string | null;
  quantityInDeck: number;
  availability: number;
  availabilityWithProxies: number;
  cardType?: string | null;
  isHorizontal?: boolean;
  isStarting?: boolean;
  cardId?: number;
  cost?: number | null;
  aspects?: Array<{ name: string; count: number }> | null;
  side?: string | null;
  setName?: string | null;
};

export type DeckValidation = {
  format: string;
  isValid: boolean;
  cardCount: number;
  basicPokemonCount: number;
  violations: string[];
};

export const deckBuilderKeys = {
  all: ["deck-builder"] as const,
  lists: () => ["deck-builder", "list"] as const,
  list: (game?: string, name?: string, page = 1, pageSize = 50) =>
    ["deck-builder", "list", { game, name, page, pageSize }] as const,
  deck: (deckId: number | null) => ["deck-builder", "meta", deckId] as const,
  cardsRoot: (deckId: number | null) => ["deck-builder", "cards", deckId] as const,
  cards: (deckId: number | null, includeProxies: boolean) =>
    ["deck-builder", "cards", deckId, { includeProxies }] as const,
  validation: (deckId: number | null) => ["deck-builder", "validation", deckId] as const,
  valueHistory: (deckId: number | null, days: number) =>
    ["deck-builder", "value-history", deckId, { days }] as const,
};

export async function fetchDecks(
  game?: string,
  name?: string,
  page = 1,
  pageSize = 50
): Promise<PagedDecks> {
  try {
    const response = await api.get<PagedDecks>("decks", {
      params: { game, name, page, pageSize },
    });
    return response.data;
  } catch (error) {
    throw toProblemDetailsError(error);
  }
}

export function useDecksListQuery(game?: string, name?: string, page = 1, pageSize = 50) {
  return useQuery({
    queryKey: deckBuilderKeys.list(game, name, page, pageSize),
    queryFn: () => fetchDecks(game, name, page, pageSize),
    staleTime: 30_000,
  });
}

export async function createDeck(payload: {
  game: string;
  name: string;
  description?: string;
  format?: string | null;
  side?: string | null;
}): Promise<DeckDetails> {
  try {
    const response = await api.post<DeckDetails>("decks", payload);
    return response.data;
  } catch (error) {
    throw toProblemDetailsError(error);
  }
}

export function useCreateDeckMutation() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: createDeck,
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: deckBuilderKeys.lists() });
    },
  });
}

export async function updateDeck(
  deckId: number,
  payload: {
    game?: string;
    name?: string;
    description?: string | null;
    format?: string | null;
    side?: string | null;
  }
): Promise<void> {
  try {
    await api.patch(`decks/${deckId}`, payload);
  } catch (error) {
    throw toProblemDetailsError(error);
  }
}

export function useUpdateDeckMutation() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({
      deckId,
      payload,
    }: {
      deckId: number;
      payload: {
        game?: string;
        name?: string;
        description?: string | null;
        format?: string | null;
        side?: string | null;
      };
    }) => updateDeck(deckId, payload),
    onSuccess: (_, { deckId }) => {
      queryClient.invalidateQueries({ queryKey: deckBuilderKeys.deck(deckId) });
      queryClient.invalidateQueries({ queryKey: deckBuilderKeys.validation(deckId) });
      queryClient.invalidateQueries({ queryKey: deckBuilderKeys.lists() });
    },
  });
}

export async function patchDeckCard(
  deckId: number,
  printingId: number,
  updates: { isStarting?: boolean; quantityInDeck?: number }
): Promise<void> {
  try {
    await api.patch(`decks/${deckId}/cards/${printingId}`, updates);
  } catch (error) {
    throw toProblemDetailsError(error);
  }
}

export function usePatchDeckCardMutation(deckId: number | null, includeProxies: boolean) {
  const queryClient = useQueryClient();
  const queryKey = deckBuilderKeys.cards(deckId, includeProxies);

  return useMutation({
    mutationFn: async ({
      printingId,
      updates,
    }: {
      printingId: number;
      updates: { isStarting?: boolean; quantityInDeck?: number };
    }) => {
      if (deckId == null) throw new Error("Deck not selected");
      await patchDeckCard(deckId, printingId, updates);
    },
    onMutate: async ({ printingId, updates }) => {
      if (deckId == null) return;
      await queryClient.cancelQueries({ queryKey });
      const previous = queryClient.getQueryData<DeckCardWithAvailability[]>(queryKey);
      if (!previous) return { previous };

      const next = previous.map((row) =>
        row.printingId === printingId ? { ...row, ...updates } : row
      );
      queryClient.setQueryData(queryKey, next);
      return { previous };
    },
    onError: (_err, _vars, context) => {
      if (context?.previous) {
        queryClient.setQueryData(queryKey, context.previous);
      }
    },
    onSettled: () => {
      if (deckId != null) {
        queryClient.invalidateQueries({ queryKey });
        queryClient.invalidateQueries({ queryKey: deckBuilderKeys.cardsRoot(deckId) });
        queryClient.invalidateQueries({ queryKey: deckBuilderKeys.deck(deckId) });
        queryClient.invalidateQueries({ queryKey: deckBuilderKeys.validation(deckId) });
      }
      queryClient.invalidateQueries({ queryKey: deckBuilderKeys.lists() });
    },
  });
}

export async function deleteDeck(deckId: number): Promise<void> {
  try {
    await api.delete(`decks/${deckId}`);
  } catch (error) {
    throw toProblemDetailsError(error);
  }
}

export function useDeleteDeckMutation() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: deleteDeck,
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: deckBuilderKeys.lists() });
    },
  });
}

export async function fetchDeckDetails(deckId: number): Promise<DeckDetails> {
  try {
    const response = await api.get<DeckDetails>(`decks/${deckId}`);
    return response.data;
  } catch (error) {
    throw toProblemDetailsError(error);
  }
}

export async function fetchDeckCards(
  deckId: number,
  includeProxies: boolean
): Promise<DeckCardWithAvailability[]> {
  try {
    const response = await api.get<DeckCardWithAvailability[]>(
      `decks/${deckId}/cards-with-availability`,
      { params: { includeProxies } }
    );
    return response.data;
  } catch (error) {
    throw toProblemDetailsError(error);
  }
}

/**
 * Fetches Pokémon deck violations from the domain service to avoid duplicating rules in the client.
 */
export async function fetchPokemonDeckValidation(deckId: number): Promise<DeckValidation> {
  try {
    const response = await api.get<DeckValidation>(`decks/${deckId}/validation`);
    return response.data;
  } catch (error) {
    throw toProblemDetailsError(error);
  }
}

/**
 * Keeps the deck builder's validation view synchronized with server-side Pokémon rules.
 */
export function usePokemonDeckValidation(deckId: number | null) {
  return useQuery({
    queryKey: deckBuilderKeys.validation(deckId),
    queryFn: () => {
      if (deckId == null) throw new Error("Deck not selected");
      return fetchPokemonDeckValidation(deckId);
    },
    enabled: deckId != null,
    staleTime: 0,
  });
}

export async function fetchDeckValueHistory(deckId: number, days: number): Promise<ValuePoint[]> {
  try {
    const response = await api.get<ValuePoint[]>(`decks/${deckId}/value/history`, {
      params: { days },
    });
    return response.data;
  } catch (error) {
    throw toProblemDetailsError(error);
  }
}

export function useDeckDetails(deckId: number | null) {
  return useQuery({
    queryKey: deckBuilderKeys.deck(deckId),
    queryFn: () => {
      if (deckId == null) throw new Error("Deck not selected");
      return fetchDeckDetails(deckId);
    },
    enabled: deckId != null,
    staleTime: 60_000,
  });
}

export function useDeckCardsWithAvailability(deckId: number | null, includeProxies: boolean) {
  return useQuery({
    queryKey: deckBuilderKeys.cards(deckId, includeProxies),
    queryFn: () => {
      if (deckId == null) throw new Error("Deck not selected");
      return fetchDeckCards(deckId, includeProxies);
    },
    enabled: deckId != null,
    staleTime: 15_000,
  });
}

export function useDeckValueHistory(deckId: number | null, days = 90) {
  return useQuery({
    queryKey: deckBuilderKeys.valueHistory(deckId, days),
    enabled: deckId != null,
    staleTime: QUERY_STALE_MS_VALUE_HISTORY,
    queryFn: () => {
      if (deckId == null) throw new Error("Deck not selected");
      return fetchDeckValueHistory(deckId, days).then((points) =>
        points.slice().sort((a, b) => a.d.localeCompare(b.d))
      );
    },
  });
}

type QuantityMutationVariables = {
  printingId: number;
  qtyDelta: number;
  cardName?: string;
  imageUrl?: string | null;
  cardType?: string | null;
  isHorizontal?: boolean;
  initialAvailability?: number;
  initialAvailabilityWithProxies?: number;
};

type QuantityDeltaPayload = Pick<QuantityMutationVariables, "printingId" | "qtyDelta">;

type QuantityMutationContext = {
  previous?: DeckCardWithAvailability[];
};

function applyQuantityDelta(
  previous: DeckCardWithAvailability[],
  variables: QuantityMutationVariables
): DeckCardWithAvailability[] {
  const { printingId, qtyDelta } = variables;
  let found = false;

  const updated = previous.map((row) => {
    if (row.printingId !== printingId) return row;
    found = true;

    const nextQty = Math.max(0, row.quantityInDeck + qtyDelta);
    const nextAvailability = Math.max(0, row.availability - qtyDelta);
    const nextAvailabilityWithProxies = Math.max(0, row.availabilityWithProxies - qtyDelta);

    return {
      ...row,
      quantityInDeck: nextQty,
      availability: nextAvailability,
      availabilityWithProxies: nextAvailabilityWithProxies,
    } satisfies DeckCardWithAvailability;
  });

  if (found) {
    return updated.filter((row) => row.printingId !== printingId || row.quantityInDeck > 0);
  }

  if (qtyDelta > 0) {
    const initialAvailability = variables.initialAvailability ?? 0;
    const initialAvailabilityWithProxies =
      variables.initialAvailabilityWithProxies ?? variables.initialAvailability ?? 0;

    return [
      ...previous,
      {
        printingId,
        cardName: variables.cardName ?? `Printing #${printingId}`,
        imageUrl: variables.imageUrl ?? null,
        cardType: variables.cardType,
        isHorizontal: variables.isHorizontal,
        quantityInDeck: qtyDelta,
        availability: Math.max(0, initialAvailability - qtyDelta),
        availabilityWithProxies: Math.max(0, initialAvailabilityWithProxies - qtyDelta),
      } satisfies DeckCardWithAvailability,
    ];
  }

  return previous;
}

export function useDeckQuantityMutation(deckId: number | null, includeProxies: boolean) {
  const queryClient = useQueryClient();
  const queryKey = deckBuilderKeys.cards(deckId, includeProxies);

  return useMutation({
    mutationFn: async (variables: QuantityMutationVariables) => {
      if (deckId == null) throw new Error("Deck not selected");
      try {
        await postDeckQuantityDelta(deckId, includeProxies, {
          printingId: variables.printingId,
          qtyDelta: variables.qtyDelta,
        });
        return variables;
      } catch (error) {
        throw toProblemDetailsError(error);
      }
    },
    onMutate: async (variables) => {
      if (deckId == null) return {} satisfies QuantityMutationContext;
      await queryClient.cancelQueries({ queryKey });
      const previous = queryClient.getQueryData<DeckCardWithAvailability[]>(queryKey);
      if (!previous) return {} satisfies QuantityMutationContext;

      const next = applyQuantityDelta(previous, variables);
      queryClient.setQueryData(queryKey, next);

      return { previous } satisfies QuantityMutationContext;
    },
    onError: (_error, _variables, context) => {
      if (context?.previous) {
        queryClient.setQueryData(queryKey, context.previous);
      }
    },
    onSettled: () => {
      if (deckId != null) {
        queryClient.invalidateQueries({ queryKey });
        queryClient.invalidateQueries({ queryKey: deckBuilderKeys.cardsRoot(deckId) });
        queryClient.invalidateQueries({ queryKey: deckBuilderKeys.deck(deckId) });
        queryClient.invalidateQueries({ queryKey: deckBuilderKeys.validation(deckId) });
      }
      queryClient.invalidateQueries({ queryKey: deckBuilderKeys.lists() });
      queryClient.invalidateQueries({ queryKey: collectionKeys.all });
    },
  });
}

export async function postDeckQuantityDelta(
  deckId: number,
  includeProxies: boolean,
  payload: QuantityDeltaPayload
) {
  try {
    await api.post(`decks/${deckId}/cards/quantity-delta`, payload, { params: { includeProxies } });
  } catch (error) {
    throw toProblemDetailsError(error);
  }
}
