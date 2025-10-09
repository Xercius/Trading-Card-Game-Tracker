import { NavLink, useLocation, useSearchParams } from "react-router-dom";
import { Input } from "@/components/ui/input";
import { Button } from "@/components/ui/button";
import { Separator } from "@/components/ui/separator";
import { Badge } from "@/components/ui/badge";
import {
  Select,
  SelectTrigger,
  SelectValue,
  SelectContent,
  SelectItem,
} from "@/components/ui/select";
import { useEffect, useMemo, useRef, useState } from "react";
import { useUser } from "@/state/useUser";
import { useQueryState } from "@/hooks/useQueryState";
import { useDebounce } from "@/hooks/useDebounce";
import { paths } from "@/routes/paths";

const GAME_OPTIONS = ["SWU", "Lorcana", "MTG", "Pokemon", "SWCCG", "FaB", "Guardians"];

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
  { to: paths.adminImport, label: "Admin · Import", requiresAdmin: true },
];

const linkCls = ({ isActive }: { isActive: boolean }) =>
  `block px-3 py-2 rounded-md text-sm font-medium ${
    isActive ? "bg-foreground text-background" : "text-foreground/70 hover:bg-foreground/10"
  }`;

export default function Header() {
  const { users, userId, setUserId } = useUser();
  const [q, setQ] = useQueryState("q", "");
  const [params, setParams] = useSearchParams();
  const location = useLocation();
  const [mobileOpen, setMobileOpen] = useState(false);
  const menuBtnRef = useRef<HTMLButtonElement>(null);
  const wasOpenRef = useRef(false);
  const [search, setSearch] = useState(q);
  const debounced = useDebounce(search, 300);

  // input -> query string
  useEffect(() => {
    if (debounced !== q) setQ(debounced);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [debounced, q]);

  // query string -> input
  useEffect(() => {
    if (q !== search) setSearch(q);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [q]);

  useEffect(() => {
    if (wasOpenRef.current && !mobileOpen) {
      menuBtnRef.current?.focus();
    }
    wasOpenRef.current = mobileOpen;
  }, [mobileOpen]);

  useEffect(() => {
    setMobileOpen(false);
  }, [location]);

  const selectedGames = useMemo(
    () => (params.get("game") ?? "").split(",").filter(Boolean),
    [params]
  );

  function toggleGame(g: string) {
    const set = new Set(selectedGames);
    if (set.has(g)) set.delete(g);
    else set.add(g);
    const next = new URLSearchParams(params);
    const val = Array.from(set).join(",");
    if (val) next.set("game", val);
    else next.delete("game");
    setParams(next, { replace: true });
  }

  function onMobileMenuKeyDown(e: React.KeyboardEvent) {
    if (e.key === "Escape") setMobileOpen(false);
  }

  const filteredLinks = useMemo(() => {
    const currentUser = users.find((u) => u.id === userId);
    return NAV_LINKS.filter((link) => (link.requiresAdmin ? currentUser?.isAdmin : true));
  }, [userId, users]);

  return (
    <header className="w-full border-b bg-background/60 backdrop-blur">
      <div className="mx-auto w-full max-w-7xl p-4 space-y-3">
        {/* Top row: brand, nav, user */}
        <div className="flex items-center gap-3">
          <div className="text-xl font-semibold">TCG Tracker</div>

          <nav className="hidden md:flex gap-1 ml-4">
            {filteredLinks.map((link) => (
              <NavLink key={link.to} to={link.to} className={linkCls}>
                {link.label}
              </NavLink>
            ))}
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
            <Select
              value={userId ? String(userId) : ""}
              onValueChange={(v) => setUserId(Number(v))}
            >
              <SelectTrigger className="w-48">
                <SelectValue placeholder="Select user" />
              </SelectTrigger>
              <SelectContent>
                {users.map((u) => (
                  <SelectItem key={u.id} value={String(u.id)}>
                    {u.name}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
            {import.meta.env.DEV && userId != null && (
              <span className="text-xs text-muted-foreground" aria-hidden>
                UserId={userId}
              </span>
            )}
          </div>
        </div>

        {/* Second row: search */}
        <div className="flex items-center gap-2">
          <Input
            placeholder="Search cards, decks..."
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            className="max-w-lg"
          />
          <Button variant="secondary" onClick={() => setSearch("")}>
            Clear
          </Button>
        </div>

        {/* Third row: filters */}
        <div className="flex items-center gap-2 flex-wrap">
          <span className="text-sm text-muted-foreground">Games:</span>
          {GAME_OPTIONS.map((g) => {
            const active = selectedGames.includes(g);
            return (
              <Badge
                key={g}
                variant={active ? "default" : "outline"}
                className="cursor-pointer"
                onClick={() => toggleGame(g)}
              >
                {g}
              </Badge>
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
              className="fixed top-0 left-0 z-50 h-full w-64 bg-background p-4 shadow-lg md:hidden"
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
            </nav>
          </>
        )}

        <Separator />
      </div>
    </header>
  );
}
