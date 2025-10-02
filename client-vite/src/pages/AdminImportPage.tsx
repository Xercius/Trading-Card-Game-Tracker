import { useQuery } from '@tanstack/react-query';
import { api } from '@/lib/api';

type ImportSourceDto = { source: string; description: string };
type Paged<T> = {
  items: T[];
  total: number;
  page: number;
  pageSize: number;
};

export default function AdminImportPage() {
  const { data, isLoading, error } = useQuery<Paged<ImportSourceDto>>({
    queryKey: ['admin-import'],
    queryFn: async () => {
      const res = await api.get<Paged<ImportSourceDto>>('/admin/import');
      return res.data;
    },
  });

  if (isLoading) return <div className="p-4">Loading…</div>;
  if (error) return <div className="p-4 text-red-500">Error loading import sources</div>;
  if (!data || data.items.length === 0) return <div className="p-4">No import sources found</div>;

  return (
    <div className="p-4">
      <div className="mb-2 text-sm text-gray-500">
        Showing {data.items.length} of {data.total}
      </div>
      <ul className="list-disc pl-6">
        {data.items.map(source => (
          <li key={source.source}>
            {source.source} — {source.description}
          </li>
        ))}
      </ul>
    </div>
  );
}

