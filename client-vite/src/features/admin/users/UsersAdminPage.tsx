import { useMemo, useState, type FormEvent } from "react";
import {
  useAdminUsersQuery,
  useCreateAdminUserMutation,
  useDeleteAdminUserMutation,
  useUpdateAdminUserMutation,
} from "@/features/admin/users/api";
import { getErrorMessage } from "@/lib/getErrorMessage";
import { useUser } from "@/state/useUser";
import type { AdminUser } from "@/types/user";

export default function UsersAdminPage() {
  const { users, userId, refreshUsers } = useUser();
  const currentUser = users.find((u) => u.id === userId);
  const isAdmin = currentUser?.isAdmin ?? false;

  const { data, isLoading, isError, refetch, isFetching } = useAdminUsersQuery(isAdmin);

  const createUser = useCreateAdminUserMutation();
  const updateUser = useUpdateAdminUserMutation();
  const deleteUser = useDeleteAdminUserMutation();

  const [newName, setNewName] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [editingId, setEditingId] = useState<number | null>(null);
  const [draftName, setDraftName] = useState("");
  const [pendingId, setPendingId] = useState<number | null>(null);
  const [pendingDeleteId, setPendingDeleteId] = useState<number | null>(null);

  const adminCount = useMemo(() => data?.filter((user) => user.isAdmin).length ?? 0, [data]);

  if (!isAdmin) {
    return <div className="p-4">Admins only.</div>;
  }

  if (isLoading) {
    return <div className="p-4">Loading…</div>;
  }

  if (isError) {
    return (
      <div className="p-4 text-red-500">
        Failed to load users. <button onClick={() => refetch()}>Retry</button>
      </div>
    );
  }

  const usersList = data ?? [];

  const handleCreate = (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    const trimmed = newName.trim();
    if (!trimmed) {
      setError("Name is required.");
      return;
    }

    setError(null);
    createUser.mutate(trimmed, {
      onSuccess: async () => {
        setNewName("");
        setError(null);
        await refreshUsers();
      },
      onError: (err) => {
        setError(getErrorMessage(err));
      },
    });
  };

  const startEdit = (user: AdminUser) => {
    setEditingId(user.id);
    setDraftName(user.displayName);
    setError(null);
  };

  const cancelEdit = () => {
    setEditingId(null);
    setDraftName("");
  };

  const handleRename = (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    if (editingId == null) return;
    const trimmed = draftName.trim();
    if (!trimmed) {
      setError("Name is required.");
      return;
    }

    setPendingId(editingId);
    updateUser.mutate(
      { id: editingId, updates: { name: trimmed } },
      {
        onSuccess: async () => {
          setError(null);
          setEditingId(null);
          setDraftName("");
          setPendingId(null);
          await refreshUsers();
        },
        onError: (err) => {
          setError(getErrorMessage(err));
          setPendingId(null);
        },
        onSettled: () => {
          setPendingId(null);
        },
      }
    );
  };

  const toggleAdmin = (user: AdminUser) => {
    const nextValue = !user.isAdmin;
    setPendingId(user.id);
    updateUser.mutate(
      { id: user.id, updates: { isAdmin: nextValue } },
      {
        onSuccess: async () => {
          setError(null);
          setPendingId(null);
          await refreshUsers();
        },
        onError: (err) => {
          setError(getErrorMessage(err));
          setPendingId(null);
        },
        onSettled: () => {
          setPendingId(null);
        },
      }
    );
  };

  const handleDelete = (user: AdminUser) => {
    if (user.isAdmin && adminCount <= 1) return;
    if (!globalThis.confirm(`Delete ${user.name}? This cannot be undone.`)) return;

    setPendingDeleteId(user.id);
    deleteUser.mutate(user.id, {
      onSuccess: async () => {
        setError(null);
        setPendingDeleteId(null);
        await refreshUsers();
      },
      onError: (err) => {
        setError(getErrorMessage(err));
        setPendingDeleteId(null);
      },
      onSettled: () => {
        setPendingDeleteId(null);
      },
    });
  };

  const isAnyMutationPending =
    createUser.isPending || updateUser.isPending || deleteUser.isPending || isFetching;

  return (
    <div className="space-y-4 p-4">
      <div>
        <h1 className="text-xl font-semibold">User management</h1>
        <p className="text-sm text-gray-500">
          Manage access to the tracker. Changes apply immediately for signed-in users.
        </p>
      </div>

      <form onSubmit={handleCreate} className="flex flex-wrap gap-2">
        <input
          type="text"
          value={newName}
          onChange={(event) => setNewName(event.target.value)}
          placeholder="New user name"
          className="flex-1 min-w-[200px] rounded border border-gray-300 px-3 py-2"
          disabled={createUser.isPending}
        />
        <button
          type="submit"
          className="rounded bg-blue-600 px-4 py-2 text-white disabled:opacity-60"
          disabled={createUser.isPending}
        >
          Add user
        </button>
      </form>

      {error ? <div className="text-sm text-red-600">{error}</div> : null}

      <div className="overflow-x-auto">
        <table className="min-w-full divide-y divide-gray-200 text-left text-sm">
          <thead className="bg-gray-50 text-xs uppercase tracking-wide text-gray-500">
            <tr>
              <th className="px-4 py-2">Name</th>
              <th className="px-4 py-2">Admin</th>
              <th className="px-4 py-2">Created</th>
              <th className="px-4 py-2">Actions</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-gray-200">
            {usersList.map((user) => {
              const disableToggle = pendingId === user.id || pendingDeleteId === user.id;
              const disableDelete =
                (user.isAdmin && adminCount <= 1) || pendingDeleteId === user.id;

              return (
                <tr key={user.id} className="bg-white">
                  <td className="px-4 py-2">
                    {editingId === user.id ? (
                      <form onSubmit={handleRename} className="flex items-center gap-2">
                        <input
                          type="text"
                          value={draftName}
                          onChange={(event) => setDraftName(event.target.value)}
                          className="flex-1 rounded border border-gray-300 px-2 py-1"
                          disabled={pendingId === user.id}
                        />
                        <button
                          type="submit"
                          className="rounded bg-green-600 px-2 py-1 text-white disabled:opacity-60"
                          disabled={pendingId === user.id}
                        >
                          Save
                        </button>
                        <button
                          type="button"
                          onClick={cancelEdit}
                          className="rounded border border-gray-300 px-2 py-1"
                          disabled={pendingId === user.id}
                        >
                          Cancel
                        </button>
                      </form>
                    ) : (
                      <div className="flex flex-col">
                        <span className="font-medium">{user.name}</span>
                        <span className="text-xs text-gray-500">{user.username}</span>
                      </div>
                    )}
                  </td>
                  <td className="px-4 py-2">
                    <input
                      type="checkbox"
                      checked={user.isAdmin}
                      onChange={() => toggleAdmin(user)}
                      disabled={disableToggle}
                    />
                  </td>
                  <td className="px-4 py-2 text-gray-600">
                    {new Date(user.createdUtc).toLocaleString()}
                  </td>
                  <td className="px-4 py-2">
                    {editingId === user.id ? null : (
                      <button
                        type="button"
                        onClick={() => startEdit(user)}
                        className="mr-2 rounded border border-gray-300 px-2 py-1 text-sm"
                        disabled={pendingId === user.id || pendingDeleteId === user.id}
                      >
                        Rename
                      </button>
                    )}
                    <button
                      type="button"
                      data-user-id={user.id}
                      onClick={() => handleDelete(user)}
                      className="rounded border border-red-500 px-2 py-1 text-sm text-red-600 disabled:opacity-60"
                      disabled={disableDelete}
                    >
                      Delete
                    </button>
                    {disableDelete && user.isAdmin && adminCount <= 1 ? (
                      <span className="ml-2 text-xs text-gray-400">Last admin</span>
                    ) : null}
                  </td>
                </tr>
              );
            })}
          </tbody>
        </table>
      </div>

      {isAnyMutationPending ? <div className="text-xs text-gray-500">Updating users…</div> : null}
    </div>
  );
}
