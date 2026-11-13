import { useId, useRef, useState, useEffect } from "react";
import { useQuery } from "@tanstack/react-query";
import { fetchPokemonNames } from "@/features/cards/api/list";
import { queryKeys } from "@/lib/queryKeys";
import { useDebounce } from "@/hooks/useDebounce";

export type EvolvesFromFilterProps = {
  value?: string;
  onChange: (value: string | undefined) => void;
  className?: string;
};

/**
 * Smart-search combobox for filtering Pokémon cards by evolutionary lineage.
 */
export function EvolvesFromFilter({ value, onChange, className = "" }: EvolvesFromFilterProps) {
  const [draft, setDraft] = useState("");
  const [isEditing, setIsEditing] = useState(false);
  const [isOpen, setIsOpen] = useState(false);
  const [highlightedIndex, setHighlightedIndex] = useState(-1);

  const containerRef = useRef<HTMLDivElement>(null);
  const inputId = useId();
  const listboxId = useId();

  const displayText = isEditing ? draft : (value ?? "");
  const trimmedTerm = draft.trim();
  const debouncedTerm = useDebounce(trimmedTerm, 250);

  const { data: names = [], isFetching } = useQuery({
    queryKey: queryKeys.pokemonNames(debouncedTerm),
    queryFn: () => fetchPokemonNames(debouncedTerm, 20),
    enabled: isEditing && debouncedTerm.length >= 2,
    staleTime: 60_000,
  });

  const results = isEditing && debouncedTerm.length >= 2 ? names : [];

  useEffect(() => {
    const handleClickOutside = (event: MouseEvent) => {
      if (containerRef.current && !containerRef.current.contains(event.target as Node)) {
        setIsOpen(false);
        setIsEditing(false);
      }
    };
    document.addEventListener("mousedown", handleClickOutside);
    return () => document.removeEventListener("mousedown", handleClickOutside);
  }, []);

  const selectName = (name: string) => {
    setIsEditing(false);
    setDraft("");
    setIsOpen(false);
    setHighlightedIndex(-1);
    onChange(name);
  };

  const clearSelection = () => {
    setIsEditing(false);
    setDraft("");
    setIsOpen(false);
    setHighlightedIndex(-1);
    onChange(undefined);
  };

  const handleKeyDown = (e: React.KeyboardEvent<HTMLInputElement>) => {
    if (!isOpen || results.length === 0) {
      if (e.key === "ArrowDown" && results.length > 0) {
        setIsOpen(true);
        setHighlightedIndex(0);
        e.preventDefault();
      }
      return;
    }

    if (e.key === "ArrowDown") {
      e.preventDefault();
      setHighlightedIndex((prev) => (prev < results.length - 1 ? prev + 1 : 0));
    } else if (e.key === "ArrowUp") {
      e.preventDefault();
      setHighlightedIndex((prev) => (prev > 0 ? prev - 1 : results.length - 1));
    } else if (e.key === "Enter" && highlightedIndex >= 0 && highlightedIndex < results.length) {
      e.preventDefault();
      selectName(results[highlightedIndex]);
    } else if (e.key === "Escape") {
      e.preventDefault();
      setIsOpen(false);
    }
  };

  return (
    <div ref={containerRef} className={`relative w-full ${className}`}>
      <label htmlFor={inputId} className="mb-1 block text-xs font-semibold text-muted-foreground">
        Evolves From
      </label>
      <div className="relative flex items-center">
        <input
          id={inputId}
          type="text"
          value={displayText}
          placeholder="Search Pokémon… (e.g. Pika)"
          onChange={(e) => {
            const next = e.target.value;
            setIsEditing(true);
            setDraft(next);
            setIsOpen(next.trim().length >= 2);
            setHighlightedIndex(0);
          }}
          onFocus={() => {
            if (isEditing && results.length > 0) setIsOpen(true);
          }}
          onKeyDown={handleKeyDown}
          className="h-8 w-full rounded-md border border-white/20 bg-card/40 px-2.5 pr-7 text-xs text-foreground shadow-sm placeholder:text-muted-foreground focus-visible:outline-none focus-visible:ring-1 focus-visible:ring-primary"
          role="combobox"
          aria-expanded={isOpen}
          aria-controls={isOpen ? listboxId : undefined}
          aria-activedescendant={
            isOpen && highlightedIndex >= 0 ? `${listboxId}-option-${highlightedIndex}` : undefined
          }
          aria-haspopup="listbox"
          aria-autocomplete="list"
        />
        {(displayText || value) && (
          <button
            type="button"
            onClick={clearSelection}
            aria-label="Clear Evolves From filter"
            className="absolute right-2 text-xs text-muted-foreground hover:text-foreground"
          >
            ×
          </button>
        )}
      </div>

      {isOpen && isEditing && debouncedTerm.length >= 2 && (
        <ul
          id={listboxId}
          role="listbox"
          className="absolute z-50 mt-1 max-h-56 w-full overflow-y-auto rounded-md border border-white/20 bg-[#1a1a1d] py-1 text-xs text-zinc-100 shadow-2xl"
        >
          {results.length === 0 ? (
            <li className="px-3 py-2 text-zinc-400">
              {isFetching ? "Searching Pokémon…" : "No matching Pokémon found"}
            </li>
          ) : (
            results.map((name, index) => (
              <li
                key={name}
                id={`${listboxId}-option-${index}`}
                role="option"
                aria-selected={index === highlightedIndex}
                onMouseEnter={() => setHighlightedIndex(index)}
                onClick={() => selectName(name)}
                className={`cursor-pointer px-3 py-1.5 transition-colors ${
                  index === highlightedIndex ? "bg-white/15 text-white" : "hover:bg-white/10"
                }`}
              >
                {name}
              </li>
            ))
          )}
        </ul>
      )}
    </div>
  );
}
