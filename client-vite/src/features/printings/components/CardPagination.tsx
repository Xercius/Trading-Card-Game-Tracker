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
        size="sm"
        className="h-8 w-8 p-0"
        onClick={() => onPageChange(1)}
        disabled={currentPage <= 1}
        aria-label="First page"
      >
        <ChevronsLeftIcon className="h-4 w-4" />
      </Button>

      {/* Previous Page (<) */}
      <Button
        variant="outline"
        size="sm"
        className="h-8 w-8 p-0"
        onClick={() => onPageChange(currentPage - 1)}
        disabled={currentPage <= 1}
        aria-label="Previous page"
      >
        <ChevronLeftIcon className="h-4 w-4" />
      </Button>

      {/* Page Selector Dropdown */}
      <div className="w-28">
        <Select
          value={String(currentPage)}
          onValueChange={(val) => {
            const p = Number(val);
            if (!Number.isNaN(p) && p >= 1 && p <= safeTotalPages) {
              onPageChange(p);
            }
          }}
        >
          <SelectTrigger aria-label="Select page" className="h-8 w-full text-xs">
            <SelectValue>{`Page ${currentPage} of ${safeTotalPages}`}</SelectValue>
          </SelectTrigger>
          <SelectContent>
            {pageOptions.map((p) => (
              <SelectItem key={p} value={String(p)}>
                Page {p} of {safeTotalPages}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>
      </div>

      {/* Next Page (>) */}
      <Button
        variant="outline"
        size="sm"
        className="h-8 w-8 p-0"
        onClick={() => onPageChange(currentPage + 1)}
        disabled={currentPage >= safeTotalPages}
        aria-label="Next page"
      >
        <ChevronRightIcon className="h-4 w-4" />
      </Button>

      {/* Last Page (>>) */}
      <Button
        variant="outline"
        size="sm"
        className="h-8 w-8 p-0"
        onClick={() => onPageChange(safeTotalPages)}
        disabled={currentPage >= safeTotalPages}
        aria-label="Last page"
      >
        <ChevronsRightIcon className="h-4 w-4" />
      </Button>

      {/* Page Size Dropdown */}
      <div className="w-24 ml-1">
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
          <SelectTrigger aria-label="Cards per page" className="h-8 w-full text-xs">
            <SelectValue>{isTableView ? "200 / page" : `${pageSize} / page`}</SelectValue>
          </SelectTrigger>
          <SelectContent>
            {isTableView ? (
              <SelectItem value="200">200 / page</SelectItem>
            ) : (
              <>
                <SelectItem value="20">20 / page</SelectItem>
                <SelectItem value="40">40 / page</SelectItem>
                <SelectItem value="100">100 / page</SelectItem>
              </>
            )}
          </SelectContent>
        </Select>
      </div>
    </div>
  );
}
