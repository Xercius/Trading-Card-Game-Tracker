import * as React from "react";
import { Button } from "@/components/ui/button";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";

export type CardPaginationProps = {
  page: number;
  totalPages: number;
  pageSize: number;
  onPageChange: (page: number) => void;
  onPageSizeChange: (pageSize: number) => void;
  isTableView?: boolean;
  className?: string;
};

function ChevronsLeftIcon({ className = "" }: { className?: string }) {
  return (
    <svg
      aria-hidden="true"
      className={className}
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      strokeWidth="2"
      strokeLinecap="round"
      strokeLinejoin="round"
    >
      <polyline points="11 17 6 12 11 7" />
      <polyline points="18 17 13 12 18 7" />
    </svg>
  );
}

function ChevronLeftIcon({ className = "" }: { className?: string }) {
  return (
    <svg
      aria-hidden="true"
      className={className}
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      strokeWidth="2"
      strokeLinecap="round"
      strokeLinejoin="round"
    >
      <polyline points="15 18 9 12 15 6" />
    </svg>
  );
}

function ChevronRightIcon({ className = "" }: { className?: string }) {
  return (
    <svg
      aria-hidden="true"
      className={className}
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      strokeWidth="2"
      strokeLinecap="round"
      strokeLinejoin="round"
    >
      <polyline points="9 18 15 12 9 6" />
    </svg>
  );
}

function ChevronsRightIcon({ className = "" }: { className?: string }) {
  return (
    <svg
      aria-hidden="true"
      className={className}
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      strokeWidth="2"
      strokeLinecap="round"
      strokeLinejoin="round"
    >
      <polyline points="13 17 18 12 13 7" />
      <polyline points="6 17 11 12 6 7" />
    </svg>
  );
}

/**
 * Accessible page navigation and page-size selector for the Cards page.
 */
export function CardPagination({
  page,
  totalPages,
  pageSize,
  onPageChange,
  onPageSizeChange,
  isTableView = false,
  className = "",
}: CardPaginationProps) {
  const safeTotalPages = Math.max(1, totalPages);
  const currentPage = Math.min(Math.max(1, page), safeTotalPages);

  const pageOptions = React.useMemo(() => {
    return Array.from({ length: safeTotalPages }, (_, i) => i + 1);
  }, [safeTotalPages]);

  return (
    <div className={`flex flex-wrap items-center gap-1.5 text-sm ${className}`.trim()}>
      {/* First Page (<<) */}
      <Button
        variant="outline"
        size="icon"
        className="h-9 w-9 p-0 px-0 shrink-0 inline-flex items-center justify-center"
        onClick={() => onPageChange(1)}
        disabled={currentPage <= 1}
        aria-label="First page"
      >
        <ChevronsLeftIcon className="h-4 w-4 shrink-0" />
      </Button>

      {/* Previous Page (<) */}
      <Button
        variant="outline"
        size="icon"
        className="h-9 w-9 p-0 px-0 shrink-0 inline-flex items-center justify-center"
        onClick={() => onPageChange(currentPage - 1)}
        disabled={currentPage <= 1}
        aria-label="Previous page"
      >
        <ChevronLeftIcon className="h-4 w-4 shrink-0" />
      </Button>

      {/* Page Selector Dropdown */}
      <div className="shrink-0">
        <Select
          value={String(currentPage)}
          onValueChange={(val) => {
            const p = Number(val);
            if (!Number.isNaN(p) && p >= 1 && p <= safeTotalPages) {
              onPageChange(p);
            }
          }}
        >
          <SelectTrigger
            aria-label="Select page"
            className="h-9 w-auto min-w-[8.5rem] px-3 text-xs whitespace-nowrap shrink-0"
          >
            <SelectValue className="whitespace-nowrap">{`Page ${currentPage} of ${safeTotalPages}`}</SelectValue>
          </SelectTrigger>
          <SelectContent className="max-h-60 min-w-[8.5rem]">
            {pageOptions.map((p) => (
              <SelectItem key={p} value={String(p)} className="text-xs py-1.5 whitespace-nowrap">
                Page {p} of {safeTotalPages}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>
      </div>

      {/* Next Page (>) */}
      <Button
        variant="outline"
        size="icon"
        className="h-9 w-9 p-0 px-0 shrink-0 inline-flex items-center justify-center"
        onClick={() => onPageChange(currentPage + 1)}
        disabled={currentPage >= safeTotalPages}
        aria-label="Next page"
      >
        <ChevronRightIcon className="h-4 w-4 shrink-0" />
      </Button>

      {/* Last Page (>>) */}
      <Button
        variant="outline"
        size="icon"
        className="h-9 w-9 p-0 px-0 shrink-0 inline-flex items-center justify-center"
        onClick={() => onPageChange(safeTotalPages)}
        disabled={currentPage >= safeTotalPages}
        aria-label="Last page"
      >
        <ChevronsRightIcon className="h-4 w-4 shrink-0" />
      </Button>

      {/* Page Size Dropdown */}
      <div className="shrink-0 ml-1">
        <Select
          value={isTableView ? "200" : String(pageSize)}
          onValueChange={(val) => {
            if (!isTableView) {
              const size = Number(val);
              if (!Number.isNaN(size)) {
                onPageSizeChange(size);
              }
            }
          }}
          disabled={isTableView}
        >
          <SelectTrigger
            aria-label="Cards per page"
            className="h-9 w-auto min-w-[7rem] px-3 text-xs whitespace-nowrap shrink-0"
          >
            <SelectValue className="whitespace-nowrap">
              {isTableView ? "200 / page" : `${pageSize} / page`}
            </SelectValue>
          </SelectTrigger>
          <SelectContent className="min-w-[7.5rem]">
            {isTableView ? (
              <SelectItem value="200" className="text-xs py-1.5 whitespace-nowrap">
                200 / page
              </SelectItem>
            ) : (
              <>
                <SelectItem value="20" className="text-xs py-1.5 whitespace-nowrap">
                  20 / page
                </SelectItem>
                <SelectItem value="40" className="text-xs py-1.5 whitespace-nowrap">
                  40 / page
                </SelectItem>
                <SelectItem value="100" className="text-xs py-1.5 whitespace-nowrap">
                  100 / page
                </SelectItem>
              </>
            )}
          </SelectContent>
        </Select>
      </div>
    </div>
  );
}
