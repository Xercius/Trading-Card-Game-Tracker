import { NavLink, useLocation, useSearchParams } from "react-router-dom";
import { Input } from "@/components/ui/input";
import { Button } from "@/components/ui/button";
import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { useUser } from "@/state/useUser";
import { useGame } from "@/state/useGame";
import { resolveImageUrl } from "@/lib/http";
import { paths } from "@/routes/paths";

type NavLinkItem = {
  to: string;
  label: string;
  requiresAdmin?: boolean;
};

const NAV_LINKS: NavLinkItem[] = [
  { to: paths.cards, label: "Cards" },
  { to: paths.collection, label: "Collection" },
  { to: paths.wishlist, label: "Wishlist" },
  { to: paths.decks, label: "Decks" },
  { to: paths.value, label: "Value" },
  { to: paths.users, label: "Users", requiresAdmin: true },
];

const IMPORTER_LINKS: NavLinkItem[] = [
  { to: paths.adminSync, label: "SWU Sync" },
  { to: paths.adminSwccgSync, label: "SWCCG Sync" },
  { to: paths.adminPokemonSync, label: "Pokémon Sync" },
];

type GameOption = {
  id: string;
  name: string;
  logo: string;
  selectable: boolean;
};

const GAME_OPTIONS: GameOption[] = [
  {
    id: "swu",
    name: "Star Wars Unlimited",
    logo: "/images/games/star_wars_unlimited_White.png",
    selectable: true,
  },
  {
    id: "swccg",
    name: "Star Wars CCG",
    logo: "/images/games/SWCCG-Logo.jpg",
    selectable: true,
  },
  {
    id: "hellbreak",
    name: "Hellbreak TCG",
    logo: "/images/games/hellbreak_logo.avif",
    selectable: false,
  },
  {
    id: "mtg",
    name: "Magic The Gathering",
    logo: "/images/games/mtg_logo.svg",
    selectable: false,
  },
  {
    id: "pokemon",
    name: "Pokemon TCG",
    logo: "/images/games/pokemon_logo.png",
    selectable: true,
  },
];

function GameLogoButton({
  game,
  isSelected,
  onSelect,
}: {
  game: GameOption;
  isSelected: boolean;
  onSelect: (gameName: string) => void;
}) {
  const [imgError, setImgError] = useState(false);
  const logoUrl = resolveImageUrl(game.logo);

  return (
    <button
      type="button"
      role="button"
      aria-label={game.name}
      aria-pressed={isSelected}
      disabled={!game.selectable}
      title={game.selectable ? game.name : `${game.name} (Coming soon)`}
      onClick={() => {
        if (game.selectable) {
          onSelect(game.name);
        }
      }}
      className={`group relative flex items-center justify-center h-12 px-3 py-1 rounded-lg border transition-all shrink-0 ${
        isSelected
          ? "border-primary bg-primary/20 ring-2 ring-primary/60 shadow-md"
          : game.selectable
            ? "border-transparent bg-transparent hover:border-border hover:bg-card/60 cursor-pointer"
            : "border-transparent bg-transparent opacity-40 hover:opacity-75 hover:border-border/40 hover:bg-card/20 cursor-not-allowed"
      }`}
    >
      {!imgError && logoUrl ? (
        <img
          src={logoUrl}
          alt={game.name}
          className={`h-8 max-h-8 w-auto max-w-[120px] object-contain pointer-events-none select-none transition-transform ${
            game.selectable ? "group-hover:scale-105" : ""
          }`}
          onError={() => setImgError(true)}
        />
      ) : (
        <span className="text-xs font-semibold text-foreground/80 whitespace-nowrap">
          {game.name}
        </span>
      )}
      {!game.selectable && <span className="sr-only"> (Coming soon)</span>}
    </button>
  );
}

const linkCls = ({ isActive }: { isActive: boolean }) =>
  `block px-3 py-2 rounded-md text-sm font-medium ${
    isActive ? "bg-foreground text-background" : "text-foreground/70 hover:bg-foreground/10"
  }`;

export default function Header() {
  const { users, userId } = useUser();
  const { activeGame, setActiveGame } = useGame();
  const [searchParams, setSearchParams] = useSearchParams();
  const q = searchParams.get("q") ?? "";
  const location = useLocation();
  const [mobileOpen, setMobileOpen] = useState(false);
  const [importersOpen, setImportersOpen] = useState(false);
  const [brandImgError, setBrandImgError] = useState(false);
  const menuBtnRef = useRef<HTMLButtonElement>(null);
  const importersDropdownRef = useRef<HTMLDivElement>(null);
  const importersTriggerRef = useRef<HTMLButtonElement>(null);
  const wasOpenRef = useRef(false);
  const [search, setSearch] = useState(q);

  const applySearch = useCallback(
    (newSearch: string) => {
      const trimmed = newSearch.trim();
      setSearchParams(
        (prev) => {
          const next = new URLSearchParams(prev);
          if (trimmed) {
            next.set("q", trimmed);
          } else {
            next.delete("q");
          }
          if (next.has("page")) {
            next.set("page", "1");
          }
          return next;
        },
        { replace: true }
      );
    },
    [setSearchParams]
  );

  // Sync external URL changes (e.g. navigation or clear filters) to local search input
  useEffect(() => {
    setSearch(q);
  }, [q]);

  // Debounced auto-sync from input to URL
  useEffect(() => {
    if (search.trim() === q.trim()) return;

    const timer = setTimeout(() => {
      applySearch(search);
    }, 300);

    return () => clearTimeout(timer);
  }, [search, q, applySearch]);

  useEffect(() => {
    if (wasOpenRef.current && !mobileOpen) {
      menuBtnRef.current?.focus();
    }
    wasOpenRef.current = mobileOpen;
  }, [mobileOpen]);

  useEffect(() => {
    setMobileOpen(false);
    setImportersOpen(false);
  }, [location.pathname]);

  // Close Importers dropdown when clicking outside or pressing Escape
  useEffect(() => {
    if (!importersOpen) return;

    function handleClickOutside(event: MouseEvent) {
      if (
        importersDropdownRef.current &&
        !importersDropdownRef.current.contains(event.target as Node)
      ) {
        setImportersOpen(false);
      }
    }

    function handleKeyDown(event: KeyboardEvent) {
      if (event.key === "Escape") {
        event.preventDefault();
        setImportersOpen(false);
        importersTriggerRef.current?.focus();
      }
    }

    document.addEventListener("mousedown", handleClickOutside);
    document.addEventListener("keydown", handleKeyDown);
    return () => {
      document.removeEventListener("mousedown", handleClickOutside);
      document.removeEventListener("keydown", handleKeyDown);
    };
  }, [importersOpen]);

  function onMobileMenuKeyDown(e: React.KeyboardEvent) {
    if (e.key === "Escape") setMobileOpen(false);
  }

  const filteredLinks = useMemo(() => {
    const currentUser = users.find((u) => u.id === userId);
    return NAV_LINKS.filter((link) => (link.requiresAdmin ? currentUser?.isAdmin : true));
  }, [userId, users]);

  const isImportersActive = useMemo(() => {
    return IMPORTER_LINKS.some((link) => {
      const target = link.to.startsWith("/") ? link.to : `/${link.to}`;
      return location.pathname === target || location.pathname.startsWith(`${target}/`);
    });
  }, [location.pathname]);

  return (
    <header className="w-full border-b bg-background/60 backdrop-blur">
      <div className="w-full px-4 sm:px-6 pt-3.5 pb-3.5">
        {/* Top row: brand, nav links, admin dropdown, search bar, owner */}
        <div className="flex items-center gap-3">
          <NavLink
            to={paths.cards}
            className="shrink-0 flex items-center focus:outline-none focus-visible:ring-2 focus-visible:ring-primary rounded"
            aria-label="TCG Tracker"
          >
            {!brandImgError ? (
              <>
                <img
                  src={resolveImageUrl("/images/tcg_tracker_logo.png")}
                  alt="TCG Tracker"
                  className="h-9 w-auto max-h-9 object-contain"
                  onError={() => setBrandImgError(true)}
                />
                <span className="sr-only">TCG Tracker</span>
              </>
            ) : (
              <span className="text-xl font-bold tracking-tight">TCG Tracker</span>
            )}
          </NavLink>

          <nav className="hidden md:flex gap-1 ml-4 items-center">
            {filteredLinks.map((link) => (
              <NavLink key={link.to} to={link.to} className={linkCls}>
                {link.label}
              </NavLink>
            ))}

            {/* Importers Dropdown */}
            <div className="relative" ref={importersDropdownRef}>
              <button
                ref={importersTriggerRef}
                type="button"
                aria-expanded={importersOpen}
                aria-haspopup="menu"
                aria-controls="importers-dropdown-menu"
                onClick={() => setImportersOpen((open) => !open)}
                className={`inline-flex items-center gap-1 px-3 py-2 rounded-md text-sm font-medium transition-colors ${
                  isImportersActive || importersOpen
                    ? "bg-foreground text-background"
                    : "text-foreground/70 hover:bg-foreground/10"
                }`}
              >
                <span>Importers</span>
                <svg
                  viewBox="0 0 20 20"
                  fill="currentColor"
                  aria-hidden="true"
                  className={`h-4 w-4 transition-transform duration-150 ${importersOpen ? "rotate-180" : ""}`}
                >
                  <path
                    fillRule="evenodd"
                    d="M5.23 7.21a.75.75 0 011.06.02L10 11.168l3.71-3.938a.75.75 0 111.08 1.04l-4.25 4.5a.75.75 0 01-1.08 0l-4.25-4.5a.75.75 0 01.02-1.06z"
                    clipRule="evenodd"
                  />
                </svg>
              </button>

              {importersOpen && (
                <div
                  id="importers-dropdown-menu"
                  role="menu"
                  aria-orientation="vertical"
                  className="absolute left-0 mt-2 w-48 rounded-lg border border-white/20 bg-[#242424] p-1 shadow-xl z-50 focus:outline-none"
                >
                  {IMPORTER_LINKS.map((link) => (
                    <NavLink
                      key={link.to}
                      to={link.to}
                      role="menuitem"
                      className={({ isActive }) =>
                        `block px-3 py-2 rounded-md text-sm font-medium transition-colors ${
                          isActive
                            ? "bg-white/15 text-white font-semibold"
                            : "text-foreground hover:bg-white/10 hover:text-white"
                        }`
                      }
                      onClick={() => setImportersOpen(false)}
                    >
                      {link.label}
                    </NavLink>
                  ))}
                </div>
              )}
            </div>

            {/* Search bar directly to the right of Admin dropdown */}
            <div className="flex items-center gap-2 ml-4">
              <Input
                placeholder="Search cards, decks..."
                value={search}
                onChange={(e) => setSearch(e.target.value)}
                onKeyDown={(e) => {
                  if (e.key === "Enter") {
                    e.preventDefault();
                    applySearch(e.currentTarget.value);
                  }
                }}
                className="w-48 lg:w-72 h-9"
              />
              <Button
                variant="secondary"
                size="sm"
                className="h-9 px-3"
                onClick={() => {
                  setSearch("");
                  applySearch("");
                }}
              >
                Clear
              </Button>
            </div>
          </nav>

          <div className="ml-auto flex items-center gap-2 shrink-0">
            <button
              ref={menuBtnRef}
              type="button"
              className="inline-flex items-center justify-center rounded-md border px-2.5 py-1.5 md:hidden"
              aria-label="Toggle navigation"
              aria-expanded={mobileOpen}
              aria-controls="mobile-nav"
              onClick={() => setMobileOpen((open) => !open)}
            >
              <svg viewBox="0 0 24 24" width="20" height="20" aria-hidden="true">
                <path
                  d="M3 6h18M3 12h18M3 18h18"
                  stroke="currentColor"
                  strokeWidth="2"
                  strokeLinecap="round"
                />
              </svg>
            </button>
            <span className="text-sm text-muted-foreground">Owner</span>
          </div>
        </div>

        {/* Second row: game logos */}
        <div
          role="region"
          aria-label="Game selection"
          className="mt-2.5 flex items-center gap-2.5 overflow-x-auto py-0.5"
        >
          {GAME_OPTIONS.map((game) => {
            const isSelected = activeGame === game.name;
            return (
              <GameLogoButton
                key={game.id}
                game={game}
                isSelected={isSelected}
                onSelect={(name) => setActiveGame(name)}
              />
            );
          })}
        </div>

        {mobileOpen && (
          <>
            <button
              type="button"
              aria-label="Close menu"
              className="fixed inset-0 z-40 bg-black/40 md:hidden"
              onClick={() => setMobileOpen(false)}
            />
            <nav
              id="mobile-nav"
              role="dialog"
              aria-modal="true"
              className="fixed top-0 left-0 z-50 h-full w-64 bg-background p-4 shadow-lg md:hidden overflow-y-auto"
              onKeyDown={onMobileMenuKeyDown}
            >
              <div className="mb-3 text-sm font-medium text-muted-foreground">Navigate</div>

              {/* Mobile search */}
              <div className="mb-4 flex items-center gap-2">
                <Input
                  placeholder="Search cards, decks..."
                  value={search}
                  onChange={(e) => setSearch(e.target.value)}
                  onKeyDown={(e) => {
                    if (e.key === "Enter") {
                      e.preventDefault();
                      applySearch(e.currentTarget.value);
                      setMobileOpen(false);
                    }
                  }}
                  className="h-9 text-sm"
                />
                <Button
                  variant="secondary"
                  size="sm"
                  className="h-9 px-3"
                  onClick={() => {
                    setSearch("");
                    applySearch("");
                  }}
                >
                  Clear
                </Button>
              </div>
              <ul className="space-y-1">
                {filteredLinks.map((link) => (
                  <li key={link.to}>
                    <NavLink to={link.to} className={linkCls}>
                      {link.label}
                    </NavLink>
                  </li>
                ))}
              </ul>

              <div className="pt-3 border-t mt-3">
                <div className="px-3 py-1 text-xs font-semibold uppercase tracking-wider text-muted-foreground">
                  Importers
                </div>
                <ul className="mt-1 space-y-1">
                  {IMPORTER_LINKS.map((link) => (
                    <li key={link.to}>
                      <NavLink to={link.to} className={linkCls}>
                        {link.label}
                      </NavLink>
                    </li>
                  ))}
                </ul>
              </div>
            </nav>
          </>
        )}
      </div>
    </header>
  );
}
