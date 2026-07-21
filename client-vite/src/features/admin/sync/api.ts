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
};

/**
 * Loads the current Star Wars Unlimited sync status for the admin tools page.
 */
export function useStarWarsUnlimitedSyncStatusQuery() {
  return useQuery<AdminSyncStatusDetails>({
    queryKey: adminSyncKeys.starWarsUnlimitedStatus(),
    queryFn: async () => {
      try {
        const response =
          await http.get<AdminSyncStatusDetails>("admin/sync/star-wars-unlimited/status");
        return response.data;
      } catch (error) {
        throw toProblemDetailsError(error);
      }
    },
  });
}

/**
 * Runs the Star Wars Unlimited admin sync and refreshes the cached status afterwards.
 */
export function useRunStarWarsUnlimitedSyncMutation() {
  const queryClient = useQueryClient();

  return useMutation<AdminSyncStatusResponse>({
    mutationFn: async () => {
      try {
        const response = await http.post<AdminSyncStatusResponse>(
          "admin/sync/star-wars-unlimited",
          null
        );
        return response.data;
      } catch (error) {
        throw toProblemDetailsError(error);
      }
    },
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: adminSyncKeys.starWarsUnlimitedStatus() });
    },
  });
}
