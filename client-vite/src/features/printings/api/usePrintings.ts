import { useQuery } from "@tanstack/react-query";
import { fetchPrintings, type PrintingQuery, type PrintingListItem } from "./printings";

export function usePrintings(query: PrintingQuery) {
  return useQuery<PrintingListItem[], Error>({
    queryKey: ["printings", query],
    queryFn: () => fetchPrintings(query),
    staleTime: 60_000,
    keepPreviousData: true,
  });
}
