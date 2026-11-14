import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import http from "@/lib/http";
import { toProblemDetailsError } from "@/lib/problemDetails";

export type AdminSyncSetHistoryEntry = {
  setCode: string;
  lastSyncedAt: string;
};

export type AdminSyncStatusDetails = {
  source: string;
  status: string;
  runningSince: string | null;
  lastCompletedAt: string | null;
  lastOutcome?: string | null;
  historyCount: number;
  history: AdminSyncSetHistoryEntry[];
  messages: string[];
};

export type AdminSyncStatusResponse = {
  source: string;
  status: string;
  startedAt: string;
  completedAt: string | null;
  setCount: number;
  created: number;
  updated: number;
  invalid: number;
  messages: string[];
};

export const adminSyncKeys = {
  all: ["admin", "sync"] as const,
  starWarsUnlimitedStatus: () => [...adminSyncKeys.all, "star-wars-unlimited", "status"] as const,
  starWarsCcgStatus: () => [...adminSyncKeys.all, "star-wars-ccg", "status"] as const,
  pokemonStatus: () => [...adminSyncKeys.all, "pokemon", "status"] as const,
};

/**
 * Loads the current Star Wars Unlimited sync status for the admin tools page.
 */
export function useStarWarsUnlimitedSyncStatusQuery() {
  return useQuery<AdminSyncStatusDetails>({
    queryKey: adminSyncKeys.starWarsUnlimitedStatus(),
    queryFn: async () => {
      try {
        const response = await http.get<AdminSyncStatusDetails>(
          "admin/sync/star-wars-unlimited/status"
        );
        return response.data;
      } catch (error) {
        throw toProblemDetailsError(error);
      }
    },
    retry: false,
  });
}

/**
 * Runs the Star Wars Unlimited admin sync and refreshes the cached status afterwards.
 */
export function useRunStarWarsUnlimitedSyncMutation() {
  const queryClient = useQueryClient();

  return useMutation<AdminSyncStatusResponse, Error, boolean | undefined>({
    mutationFn: async (forceFullSync) => {
      try {
        const response = await http.post<AdminSyncStatusResponse>(
          forceFullSync
            ? "admin/sync/star-wars-unlimited?forceFullSync=true"
            : "admin/sync/star-wars-unlimited",
          null
        );
        return response.data;
      } catch (error) {
        throw toProblemDetailsError(error);
      }
    },
    onSettled: async () => {
      await queryClient.invalidateQueries({ queryKey: adminSyncKeys.starWarsUnlimitedStatus() });
    },
  });
}

export type SwccgSyncOptions = {
  dryRun?: boolean;
  limit?: number;
};

/**
 * Loads the current Star Wars CCG sync status for the admin tools page.
 */
export function useStarWarsCcgSyncStatusQuery() {
  return useQuery<AdminSyncStatusDetails>({
    queryKey: adminSyncKeys.starWarsCcgStatus(),
    queryFn: async () => {
      try {
        const response = await http.get<AdminSyncStatusDetails>("admin/sync/star-wars-ccg/status");
        return response.data;
      } catch (error) {
        throw toProblemDetailsError(error);
      }
    },
    retry: false,
  });
}

/**
 * Runs the Star Wars CCG admin sync and refreshes the cached status afterwards.
 */
export function useRunStarWarsCcgSyncMutation() {
  const queryClient = useQueryClient();

  return useMutation<AdminSyncStatusResponse, Error, SwccgSyncOptions | undefined>({
    mutationFn: async (options) => {
      try {
        const response = await http.post<AdminSyncStatusResponse>(
          "admin/sync/star-wars-ccg",
          options ?? {}
        );
        return response.data;
      } catch (error) {
        throw toProblemDetailsError(error);
      }
    },
    onSettled: async () => {
      await queryClient.invalidateQueries({ queryKey: adminSyncKeys.starWarsCcgStatus() });
    },
  });
}

export type AdminPokemonSetEntry = {
  id: number;
  code: string;
  name: string;
  era: string | null;
  releaseDate: string | null;
  cardCountInDb: number;
  isSynced: boolean;
  lastSyncedAt: string | null;
};

export type AdminPokemonSyncStatusResponse = {
  isRunning: boolean;
  runningSince: string | null;
  lastSetsSyncAt: string | null;
  lastCardsSyncAt: string | null;
  lastOutcome: string | null;
  sets: AdminPokemonSetEntry[];
};

export type AdminPokemonCardsSyncRequest = {
  setCodes: string[];
  forceFullSync?: boolean;
  dryRun?: boolean;
};

export type ImportSummaryDto = {
  source: string;
  dryRun: boolean;
  cardsCreated: number;
  cardsUpdated: number;
  printingsCreated: number;
  printingsUpdated: number;
  errors: number;
  messages: string[];
};

/**
 * Loads the current Pokémon TCG sync status and set list for the admin tools page.
 */
export function usePokemonSyncStatusQuery() {
  return useQuery<AdminPokemonSyncStatusResponse>({
    queryKey: adminSyncKeys.pokemonStatus(),
    queryFn: async () => {
      try {
        const response = await http.get<AdminPokemonSyncStatusResponse>(
          "admin/sync/pokemon/status"
        );
        return response.data;
      } catch (error) {
        throw toProblemDetailsError(error);
      }
    },
    retry: false,
  });
}

/**
 * Syncs all Pokémon sets and eras from TCGdex.
 */
export function useRunPokemonSetsSyncMutation() {
  const queryClient = useQueryClient();

  return useMutation<ImportSummaryDto, Error, void>({
    mutationFn: async () => {
      try {
        const response = await http.post<ImportSummaryDto>("admin/sync/pokemon/sets", null);
        return response.data;
      } catch (error) {
        throw toProblemDetailsError(error);
      }
    },
    onSettled: async () => {
      await queryClient.invalidateQueries({ queryKey: adminSyncKeys.pokemonStatus() });
    },
  });
}

/**
 * Syncs Pokémon cards for the specified set codes.
 */
export function useRunPokemonCardsSyncMutation() {
  const queryClient = useQueryClient();

  return useMutation<ImportSummaryDto, Error, AdminPokemonCardsSyncRequest>({
    mutationFn: async (payload) => {
      try {
        const response = await http.post<ImportSummaryDto>("admin/sync/pokemon/cards", payload);
        return response.data;
      } catch (error) {
        throw toProblemDetailsError(error);
      }
    },
    onSettled: async () => {
      await queryClient.invalidateQueries({ queryKey: adminSyncKeys.pokemonStatus() });
    },
  });
}
