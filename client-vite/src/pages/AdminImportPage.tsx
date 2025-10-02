import { useQuery } from '@tanstack/react-query';
import { api } from '@/lib/api';

type ImportSourceDto = { key: string; name: string; games: string[] };

export default function AdminImportPage() {
  const { data, isLoading, isError } = useQuery<ImportSourceDto[]>({
    queryKey: ['import-sources'],
    queryFn: () => api.get<ImportSourceDto[]>('/admin/import/sources').then(r => r.data),
  });

  if (isLoading) return <div className="p-4">Loading…</div>;
  if (isError) return <div className="p-4 text-red-500">Error loading import sources</div>;
  if (!data || data.length === 0) return <div className="p-4">No import sources found</div>;

  return (
    <div className="p-4">
      <div className="mb-2 text-sm text-gray-500">
        Showing {data.length} import source{data.length === 1 ? '' : 's'}
      </div>
      <ul className="list-disc pl-6">
        {data.map(source => (
          <li key={source.key}>
            {source.name}
            {source.games.length > 0 ? ` — ${source.games.join(', ')}` : ''}
          </li>
        ))}
      </ul>
    </div>
  );
}

