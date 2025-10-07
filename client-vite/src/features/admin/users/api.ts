import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import http from "@/lib/http";
import { mapAdminUser } from "@/lib/mapUser";
import type { AdminUser, AdminUserApi } from "@/types/user";

export const adminUsersKeys = {
  all: ["admin", "users"] as const,
};

export function useAdminUsersQuery(enabled: boolean) {
  return useQuery<AdminUser[]>({
    queryKey: adminUsersKeys.all,
    queryFn: async () => {
      const response = await http.get<AdminUserApi[]>("admin/users");
      return response.data.map(mapAdminUser);
    },
    enabled,
  });
}

let tempUserId = -1;

type CreateContext = { previous: AdminUser[]; tempId: number } | undefined;

export function useCreateAdminUserMutation() {
  const queryClient = useQueryClient();

  return useMutation<AdminUser, unknown, string, CreateContext>({
    mutationFn: async (name: string) => {
      const response = await http.post<AdminUserApi>("admin/users", { name });
      return mapAdminUser(response.data);
    },
    onMutate: async (name: string) => {
      await queryClient.cancelQueries({ queryKey: adminUsersKeys.all });
      const previous = queryClient.getQueryData<AdminUser[]>(adminUsersKeys.all) ?? [];
      const trimmed = name.trim();
      const optimistic: AdminUser = {
        id: tempUserId--,
        name: trimmed,
        username: trimmed,
        displayName: trimmed,
        isAdmin: false,
        createdUtc: new Date().toISOString(),
      };
      queryClient.setQueryData<AdminUser[]>(adminUsersKeys.all, [...previous, optimistic]);
      return { previous, tempId: optimistic.id };
    },
    onError: (_error, _name, context) => {
      if (context?.previous) {
        queryClient.setQueryData(adminUsersKeys.all, context.previous);
      }
    },
    onSuccess: (user, _name, context) => {
      if (!context) {
        queryClient.setQueryData<AdminUser[]>(adminUsersKeys.all, (prev) => {
          const next = prev ? [...prev, user] : [user];
          return next;
        });
        return;
      }

      queryClient.setQueryData<AdminUser[]>(adminUsersKeys.all, (prev) => {
        if (!prev) return [user];
        return prev.map((existing) => (existing.id === context.tempId ? user : existing));
      });
    },
    onSettled: () => {
      queryClient.invalidateQueries({ queryKey: adminUsersKeys.all });
    },
  });
}

type UpdateInput = {
  id: number;
  updates: {
    name?: string;
    isAdmin?: boolean;
  };
};

type UpdateContext = { previous: AdminUser[] } | undefined;

export function useUpdateAdminUserMutation() {
  const queryClient = useQueryClient();

  return useMutation<AdminUser, unknown, UpdateInput, UpdateContext>({
    mutationFn: async ({ id, updates }) => {
      const response = await http.put<AdminUserApi>(`admin/users/${id}`, updates);
      return mapAdminUser(response.data);
    },
    onMutate: async ({ id, updates }) => {
      await queryClient.cancelQueries({ queryKey: adminUsersKeys.all });
      const previous = queryClient.getQueryData<AdminUser[]>(adminUsersKeys.all) ?? [];

      const hasName = Object.prototype.hasOwnProperty.call(updates, "name");
      const trimmedName = updates.name?.trim();
      const hasAdmin = Object.prototype.hasOwnProperty.call(updates, "isAdmin");

      queryClient.setQueryData<AdminUser[]>(adminUsersKeys.all, (prev) => {
        const current = prev ?? previous;
        return current.map((user) => {
          if (user.id !== id) return user;
          let next = { ...user };
          if (hasName) {
            const finalName = trimmedName && trimmedName.length > 0 ? trimmedName : user.name;
            next = {
              ...next,
              name: finalName,
              username: finalName,
              displayName: finalName,
            };
          }
          if (hasAdmin && typeof updates.isAdmin === "boolean") {
            next = { ...next, isAdmin: updates.isAdmin };
          }
          return next;
        });
      });

      return { previous };
    },
    onError: (_error, _input, context) => {
      if (context?.previous) {
        queryClient.setQueryData(adminUsersKeys.all, context.previous);
      }
    },
    onSuccess: (user) => {
      queryClient.setQueryData<AdminUser[]>(adminUsersKeys.all, (prev) => {
        if (!prev) return [user];
        return prev.map((existing) => (existing.id === user.id ? user : existing));
      });
    },
    onSettled: () => {
      queryClient.invalidateQueries({ queryKey: adminUsersKeys.all });
    },
  });
}

type DeleteContext = { previous: AdminUser[] } | undefined;

export function useDeleteAdminUserMutation() {
  const queryClient = useQueryClient();

  return useMutation<void, unknown, number, DeleteContext>({
    mutationFn: async (id: number) => {
      await http.delete(`admin/users/${id}`);
    },
    onMutate: async (id: number) => {
      await queryClient.cancelQueries({ queryKey: adminUsersKeys.all });
      const previous = queryClient.getQueryData<AdminUser[]>(adminUsersKeys.all) ?? [];
      queryClient.setQueryData<AdminUser[]>(adminUsersKeys.all, (prev) => {
        const current = prev ?? previous;
        return current.filter((user) => user.id !== id);
      });
      return { previous };
    },
    onError: (_error, _id, context) => {
      if (context?.previous) {
        queryClient.setQueryData(adminUsersKeys.all, context.previous);
      }
    },
    onSettled: () => {
      queryClient.invalidateQueries({ queryKey: adminUsersKeys.all });
    },
  });
}
