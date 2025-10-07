import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { api } from "@/lib/api";
import { collectionKeys } from "@/features/collection/api";
import { QUERY_STALE_MS_VALUE_HISTORY } from "@/constants";
import type { ValuePoint } from "@/types/value";

export type DeckDetails = {
  id: number;
  userId: number;
  game: string;
  name: string;
  description: string | null;
  createdUtc: string;
  updatedUtc: string | null;
};

export type DeckCardWithAvailability = {
  printingId: number;
  cardName: string;
  imageUrl: string | null;
  quantityInDeck: number;
  availability: number;
  availabilityWithProxies: number;
};

export const deckBuilderKeys = {
  all: ["deck-builder"] as const,
  deck: (deckId: number | null) => ["deck-builder", "meta", deckId] as const,
  cardsRoot: (deckId: number | null) => ["deck-builder", "cards", deckId] as const,
  cards: (deckId: number | null, includeProxies: boolean) =>
    ["deck-builder", "cards", deckId, { includeProxies }] as const,
  valueHistory: (deckId: number | null, days: number) =>
    ["deck-builder", "value-history", deckId, { days }] as const,
};

async function fetchDeckDetails(deckId: number): Promise<DeckDetails> {
  const response = await api.get<DeckDetails>(`decks/${deckId}`);
  return response.data;
}

async function fetchDeckCards(deckId: number, includeProxies: boolean): Promise<DeckCardWithAvailability[]> {
  const response = await api.get<DeckCardWithAvailability[]>(
    `decks/${deckId}/cards-with-availability`,
    { params: { includeProxies } }
  );
  return response.data;
}

async function fetchDeckValueHistory(deckId: number, days: number): Promise<ValuePoint[]> {
  const response = await api.get<ValuePoint[]>(`decks/${deckId}/value/history`, {
    params: { days },
  });
  return response.data;
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
      await postDeckQuantityDelta(deckId, includeProxies, {
        printingId: variables.printingId,
        qtyDelta: variables.qtyDelta,
      });
      return variables;
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
      }
      queryClient.invalidateQueries({ queryKey: collectionKeys.all });
    },
  });
}

export async function postDeckQuantityDelta(
  deckId: number,
  includeProxies: boolean,
  payload: QuantityDeltaPayload
) {
  await api.post(
    `decks/${deckId}/cards/quantity-delta`,
    payload,
    { params: { includeProxies } }
  );
}
