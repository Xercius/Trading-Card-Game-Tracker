import { useState, useEffect, useRef, useCallback, useId } from "react";
import { useQuery } from "@tanstack/react-query";
import { fetchPrintings, type PrintingListItem } from "@/features/printings/api/printings";
import { useGame } from "@/state/useGame";
import { resolveImageUrl } from "@/lib/http";
import { useDebounce } from "@/hooks/useDebounce";

type MoxfieldCardSearchProps = {
  placeholder?: string;
  onSelectPrinting: (printing: PrintingListItem) => void;
  className?: string;
};

export default function MoxfieldCardSearch({
  placeholder = "Search card to add… (type name, set, or #)",
  onSelectPrinting,
  className = "",
}: MoxfieldCardSearchProps) {
  const { activeGame } = useGame();
  const [searchTerm, setSearchTerm] = useState("");
  const debouncedTerm = useDebounce(searchTerm.trim(), 250);
  const [isOpen, setIsOpen] = useState(false);
  const [highlightedIndex, setHighlightedIndex] = useState(-1);

  const containerRef = useRef<HTMLDivElement>(null);
  const listRef = useRef<HTMLUListElement>(null);
  const listboxId = useId();

  const { data: searchData, isFetching: isLoading } = useQuery({
    queryKey: ["printings-quick-search", activeGame ?? "all", debouncedTerm],
    queryFn: () =>
      fetchPrintings({
        q: debouncedTerm,
        game: activeGame ? [activeGame] : undefined,
        pageSize: 15,
      }),
    enabled: debouncedTerm.length >= 2,
    staleTime: 30_000,
  });

  const results = debouncedTerm.length >= 2 ? (searchData?.items ?? []) : [];

  useEffect(() => {
    if (debouncedTerm.length >= 2 && searchData) {
      setIsOpen(true);
      setHighlightedIndex(0);
    } else if (debouncedTerm.length < 2) {
      setIsOpen(false);
    }
  }, [debouncedTerm, searchData]);

  // Close dropdown when clicking outside
  useEffect(() => {
    const handleClickOutside = (event: MouseEvent) => {
      if (containerRef.current && !containerRef.current.contains(event.target as Node)) {
        setIsOpen(false);
      }
    };
    document.addEventListener("mousedown", handleClickOutside);
    return () => document.removeEventListener("mousedown", handleClickOutside);
  }, []);

  const handleSelect = useCallback(
    (item: PrintingListItem) => {
      onSelectPrinting(item);
      setIsOpen(false);
      setSearchTerm("");
    },
    [onSelectPrinting]
  );

  const handleKeyDown = (e: React.KeyboardEvent<HTMLInputElement>) => {
    if (!isOpen || results.length === 0) {
      if (e.key === "ArrowDown" && results.length > 0) {
        setIsOpen(true);
        e.preventDefault();
      }
      return;
    }

    if (e.key === "ArrowDown") {
      e.preventDefault();
      setHighlightedIndex((prev) => {
        const next = prev < results.length - 1 ? prev + 1 : 0;
        scrollIntoView(next);
        return next;
      });
    } else if (e.key === "ArrowUp") {
      e.preventDefault();
      setHighlightedIndex((prev) => {
        const next = prev > 0 ? prev - 1 : results.length - 1;
        scrollIntoView(next);
        return next;
      });
    } else if (e.key === "Enter") {
      e.preventDefault();
      if (highlightedIndex >= 0 && highlightedIndex < results.length) {
        handleSelect(results[highlightedIndex]);
      }
    } else if (e.key === "Escape") {
      e.preventDefault();
      setIsOpen(false);
    }
  };

  const scrollIntoView = (index: number) => {
    if (!listRef.current) return;
    const item = listRef.current.children[index] as HTMLElement | undefined;
    if (item) {
      item.scrollIntoView({ block: "nearest" });
    }
  };

  return (
    <div ref={containerRef} className={`relative w-full max-w-md ${className}`}>
      <div className="relative flex items-center">
        <input
          type="text"
          value={searchTerm}
          onChange={(e) => setSearchTerm(e.target.value)}
          onFocus={() => {
            if (results.length > 0) setIsOpen(true);
          }}
          onKeyDown={handleKeyDown}
          placeholder={placeholder}
          className="h-9 w-full rounded-md border border-white/20 bg-[#18181b] pl-9 pr-8 text-sm text-white shadow-sm transition-colors placeholder:text-zinc-400 focus-visible:outline-none focus-visible:ring-1 focus-visible:ring-blue-500"
          role="combobox"
          aria-expanded={isOpen}
          aria-controls={isOpen ? listboxId : undefined}
          aria-activedescendant={
            isOpen && highlightedIndex >= 0 ? `${listboxId}-option-${highlightedIndex}` : undefined
          }
          aria-haspopup="listbox"
          aria-autocomplete="list"
        />
        {/* Search Icon */}
        <span className="pointer-events-none absolute left-2.5 text-zinc-400">
          {isLoading ? (
            <span className="inline-block h-4 w-4 animate-spin rounded-full border-2 border-primary border-t-transparent" />
          ) : (
            <svg
              className="h-4 w-4"
              xmlns="http://www.w3.org/2000/svg"
              fill="none"
              viewBox="0 0 24 24"
              stroke="currentColor"
            >
              <path
                strokeLinecap="round"
                strokeLinejoin="round"
                strokeWidth={2}
                d="M21 21l-6-6m2-5a7 7 0 11-14 0 7 7 0 0114 0z"
              />
            </svg>
          )}
        </span>

        {/* Clear Button */}
        {searchTerm && (
          <button
            type="button"
            onClick={() => {
              setSearchTerm("");
              setIsOpen(false);
            }}
            className="absolute right-2 text-zinc-400 hover:text-white"
            aria-label="Clear search"
          >
            ×
          </button>
        )}
      </div>

      {/* Autocomplete Results Dropdown (Moxfield Style) */}
      {isOpen && (
        <ul
          ref={listRef}
          id={listboxId}
          role="listbox"
          className="absolute z-50 mt-1 max-h-72 w-full overflow-y-auto rounded-md border border-white/20 bg-[#1a1a1d] py-1 text-sm text-zinc-100 shadow-2xl focus:outline-none"
        >
          {results.length === 0 ? (
            <li className="px-3 py-2 text-xs text-zinc-400">
              {isLoading ? "Searching cards…" : "No matching cards found"}
            </li>
          ) : (
            results.map((item, index) => {
              const isSelected = index === highlightedIndex;
              const setDisplay = item.setCode
                ? `[${item.setCode.toUpperCase()}]`
                : `[${item.setName}]`;
              const numDisplay = item.number ? `#${item.number}` : "";
              const styleDisplay = item.style && item.style !== "Standard" ? `(${item.style})` : "";

              return (
                <li
                  key={String(item.printingId)}
                  id={`${listboxId}-option-${index}`}
                  role="option"
                  aria-selected={isSelected}
                  onMouseEnter={() => setHighlightedIndex(index)}
                  onClick={() => handleSelect(item)}
                  className={`flex cursor-pointer items-center justify-between gap-2 px-3 py-2 transition-colors ${
                    isSelected ? "bg-white/15 text-white" : "hover:bg-white/10 text-zinc-200"
                  }`}
                >
                  <div className="flex min-w-0 items-center gap-2">
                    {item.imageUrl ? (
                      <img
                        src={resolveImageUrl(item.imageUrl)}
                        alt=""
                        className="h-8 w-6 shrink-0 rounded object-cover border border-white/10"
                        loading="lazy"
                        decoding="async"
                      />
                    ) : (
                      <div className="h-8 w-6 shrink-0 rounded bg-zinc-800 border border-white/10 flex items-center justify-center text-[10px] text-zinc-400">
                        {item.game?.slice(0, 3).toUpperCase() || "TCG"}
                      </div>
                    )}
                    <div className="min-w-0 flex-1">
                      <div className="truncate font-medium">
                        {item.cardName}{" "}
                        {styleDisplay && (
                          <span className="text-xs text-zinc-400 font-normal">{styleDisplay}</span>
                        )}
                      </div>
                      <div className="flex items-center gap-1.5 text-xs text-zinc-400">
                        <span className="font-semibold text-zinc-300">{setDisplay}</span>
                        {numDisplay && <span>{numDisplay}</span>}
                        {item.rarity && <span>· {item.rarity}</span>}
                      </div>
                    </div>
                  </div>
                </li>
              );
            })
          )}
        </ul>
      )}
    </div>
  );
}
