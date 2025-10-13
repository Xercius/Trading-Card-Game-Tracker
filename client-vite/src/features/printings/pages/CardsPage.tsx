import * as React from "react";
import { useDebouncedCallback } from "use-debounce";
import { usePrintings } from "../api/usePrintings";
import type { PrintingQuery } from "../api/printings";
import { PrintingCard } from "../components/PrintingCard";

export default function CardsPage() {
  const [query, setQuery] = React.useState<PrintingQuery>({
    q: "",
    game: [],
    set: [],
    rarity: [],
    page: 1,
    pageSize: 60,
  });

  const { data, isLoading, isError, error } = usePrintings(query);
  const printings = data ?? [];

  const setSearch = useDebouncedCallback((q: string) => {
    setQuery(prev => ({ ...prev, q, page: 1 }));
  }, 250);

  return (
    <div className="p-4 md:p-6 space-y-4">
      <div className="flex flex-col gap-3 md:flex-row md:items-center md:justify-between">
        <input
          type="search"
          placeholder="Search name or text…"
          defaultValue={query.q}
          onChange={(e) => setSearch(e.target.value)}
          className="w-full md:w-80 rounded-md border px-3 py-2"
        />
        {/* TODO: wire shadcn/ui selects for Game/Set/Rarity → update query.game/set/rarity */}
      </div>

      {isLoading && <div className="text-sm text-muted-foreground">Loading printings…</div>}
      {isError && <div className="text-sm text-destructive">Error: {error?.message}</div>}

      {!isLoading && !isError && (
        <>
          <div className="text-sm text-muted-foreground">{printings.length} printings</div>
          <ul className="grid grid-cols-2 sm:grid-cols-3 md:grid-cols-4 lg:grid-cols-6 gap-3">
            {printings.map(p => (
              <li key={p.printingId}>
                <PrintingCard p={p} />
              </li>
            ))}
          </ul>
        </>
      )}
    </div>
  );
}
