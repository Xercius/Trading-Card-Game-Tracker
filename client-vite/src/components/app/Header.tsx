import { NavLink, useLocation, useSearchParams } from "react-router-dom";
import { Input } from "@/components/ui/input";
import { Button } from "@/components/ui/button";
import { Separator } from "@/components/ui/separator";
import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { useUser } from "@/state/useUser";
import { useGame } from "@/state/useGame";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
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

const ADMIN_LINKS: NavLinkItem[] = [
  { to: paths.adminSync, label: "SWU Sync" },
  { to: paths.adminImport, label: "Admin · Import" },
];

const linkCls = ({ isActive }: { isActive: boolean }) =>
  `block px-3 py-2 rounded-md text-sm font-medium ${
    isActive ? "bg-foreground text-background" : "text-foreground/70 hover:bg-foreground/10"
  }`;

export default function Header() {
  const { users, userId } = useUser();
  const { activeGame, setActiveGame, games } = useGame();
  const [searchParams, setSearchParams] = useSearchParams();
  const q = searchParams.get("q") ?? "";
  const location = useLocation();
  const [mobileOpen, setMobileOpen] = useState(false);
  const [adminOpen, setAdminOpen] = useState(false);
  const menuBtnRef = useRef<HTMLButtonElement>(null);
  const adminDropdownRef = useRef<HTMLDivElement>(null);
  const adminTriggerRef = useRef<HTMLButtonElement>(null);
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
    setAdminOpen(false);
  }, [location]);

  // Close Admin dropdown when clicking outside or pressing Escape
  useEffect(() => {
    if (!adminOpen) return;

    function handleClickOutside(event: MouseEvent) {
      if (adminDropdownRef.current && !adminDropdownRef.current.contains(event.target as Node)) {
        setAdminOpen(false);
      }
    }

    function handleKeyDown(event: KeyboardEvent) {
      if (event.key === "Escape") {
        event.preventDefault();
        setAdminOpen(false);
        adminTriggerRef.current?.focus();
      }
    }

    document.addEventListener("mousedown", handleClickOutside);
    document.addEventListener("keydown", handleKeyDown);
    return () => {
      document.removeEventListener("mousedown", handleClickOutside);
      document.removeEventListener("keydown", handleKeyDown);
    };
  }, [adminOpen]);

  function onMobileMenuKeyDown(e: React.KeyboardEvent) {
    if (e.key === "Escape") setMobileOpen(false);
  }

  const filteredLinks = useMemo(() => {
    const currentUser = users.find((u) => u.id === userId);
    return NAV_LINKS.filter((link) => (link.requiresAdmin ? currentUser?.isAdmin : true));
  }, [userId, users]);

  const isAdminActive = useMemo(() => {
    return ADMIN_LINKS.some((link) => {
      const target = link.to.startsWith("/") ? link.to : `/${link.to}`;
      return location.pathname === target || location.pathname.startsWith(`${target}/`);
    });
  }, [location.pathname]);

  return (
    <header className="w-full border-b bg-background/60 backdrop-blur">
      <div className="w-full px-4 sm:px-6 py-4 space-y-3">
        {/* Top row: brand, nav, owner */}
        <div className="flex items-center gap-3">
          <div className="text-xl font-semibold">TCG Tracker</div>

          <nav className="hidden md:flex gap-1 ml-4 items-center">
            {filteredLinks.map((link) => (
              <NavLink key={link.to} to={link.to} className={linkCls}>
                {link.label}
              </NavLink>
            ))}

            {/* Admin Dropdown */}
            <div className="relative" ref={adminDropdownRef}>
              <button
                ref={adminTriggerRef}
                type="button"
                aria-expanded={adminOpen}
                aria-haspopup="menu"
                aria-controls="admin-dropdown-menu"
                onClick={() => setAdminOpen((open) => !open)}
                className={`inline-flex items-center gap-1 px-3 py-2 rounded-md text-sm font-medium transition-colors ${
                  isAdminActive || adminOpen
                    ? "bg-foreground text-background"
                    : "text-foreground/70 hover:bg-foreground/10"
                }`}
              >
                <span>Admin</span>
                <svg
                  viewBox="0 0 20 20"
                  fill="currentColor"
                  aria-hidden="true"
                  className={`h-4 w-4 transition-transform duration-150 ${adminOpen ? "rotate-180" : ""}`}
                >
                  <path
                    fillRule="evenodd"
                    d="M5.23 7.21a.75.75 0 011.06.02L10 11.168l3.71-3.938a.75.75 0 111.08 1.04l-4.25 4.5a.75.75 0 01-1.08 0l-4.25-4.5a.75.75 0 01.02-1.06z"
                    clipRule="evenodd"
                  />
                </svg>
              </button>

              {adminOpen && (
                <div
                  id="admin-dropdown-menu"
                  role="menu"
                  aria-orientation="vertical"
                  className="absolute left-0 mt-2 w-48 rounded-lg border border-white/20 bg-[#242424] p-1 shadow-xl z-50 focus:outline-none"
                >
                  {ADMIN_LINKS.map((link) => (
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
                      onClick={() => setAdminOpen(false)}
                    >
                      {link.label}
                    </NavLink>
                  ))}
                </div>
              )}
            </div>
          </nav>

          <div className="ml-auto flex items-center gap-2">
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

        {/* Second row: game filter + search */}
        <div className="flex items-center gap-2">
          <div className="w-52 shrink-0">
            <Select
              value={activeGame}
              onValueChange={(val) => {
                if (val && val.trim()) {
                  setActiveGame(val.trim());
                }
              }}
            >
              <SelectTrigger aria-label="Game" className="w-full">
                <SelectValue placeholder="Select game" />
              </SelectTrigger>
              <SelectContent>
                {games.map((gameName) => (
                  <SelectItem key={gameName} value={gameName}>
                    {gameName}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>
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
            className="max-w-lg"
          />
          <Button
            variant="secondary"
            onClick={() => {
              setSearch("");
              applySearch("");
            }}
          >
            Clear
          </Button>
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
                  Admin
                </div>
                <ul className="mt-1 space-y-1">
                  {ADMIN_LINKS.map((link) => (
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

        <Separator />
      </div>
    </header>
  );
}
