import { useEffect, useState, useCallback } from "react";
import {
  usePrintingQuantitiesQuery,
  useSetCardQuantitiesMutation,
} from "@/features/collection/api";

type CardQuantitiesEditorProps = {
  printingId: number | null | undefined;
};

function normalizeQty(val: string): number {
  const parsed = parseInt(val, 10);
  return Number.isFinite(parsed) && parsed >= 0 ? parsed : 0;
}

export default function CardQuantitiesEditor({ printingId }: CardQuantitiesEditorProps) {
  const { data: quantities, isLoading } = usePrintingQuantitiesQuery(printingId);
  const mutation = useSetCardQuantitiesMutation();

  const [ownedDraft, setOwnedDraft] = useState("0");
  const [proxyDraft, setProxyDraft] = useState("0");
  const [wishlistDraft, setWishlistDraft] = useState("0");

  useEffect(() => {
    if (quantities) {
      setOwnedDraft(String(quantities.quantityOwned ?? 0));
      setProxyDraft(String(quantities.quantityProxyOwned ?? 0));
      setWishlistDraft(String(quantities.quantityWanted ?? 0));
    } else {
      setOwnedDraft("0");
      setProxyDraft("0");
      setWishlistDraft("0");
    }
  }, [quantities, printingId]);

  const commitChanges = useCallback(
    (newOwned?: number, newProxy?: number, newWishlist?: number) => {
      if (!printingId) return;

      const owned = newOwned !== undefined ? newOwned : normalizeQty(ownedDraft);
      const proxy = newProxy !== undefined ? newProxy : normalizeQty(proxyDraft);
      const wanted = newWishlist !== undefined ? newWishlist : normalizeQty(wishlistDraft);

      if (
        quantities &&
        owned === quantities.quantityOwned &&
        proxy === quantities.quantityProxyOwned &&
        wanted === quantities.quantityWanted
      ) {
        return;
      }

      mutation.mutate({
        printingId,
        quantityOwned: owned,
        quantityProxyOwned: proxy,
        quantityWanted: wanted,
      });
    },
    [printingId, ownedDraft, proxyDraft, wishlistDraft, quantities, mutation]
  );

  const handleStep = (field: "owned" | "proxy" | "wishlist", delta: number) => {
    if (field === "owned") {
      const next = Math.max(0, normalizeQty(ownedDraft) + delta);
      setOwnedDraft(String(next));
      commitChanges(next, undefined, undefined);
    } else if (field === "proxy") {
      const next = Math.max(0, normalizeQty(proxyDraft) + delta);
      setProxyDraft(String(next));
      commitChanges(undefined, next, undefined);
    } else {
      const next = Math.max(0, normalizeQty(wishlistDraft) + delta);
      setWishlistDraft(String(next));
      commitChanges(undefined, undefined, next);
    }
  };

  if (!printingId) return null;

  return (
    <div
      className="rounded-lg border border-white/20 bg-[#1c1c1f] p-2.5 text-xs shrink-0"
      data-testid="card-quantities-editor"
    >
      <div className="grid grid-cols-3 gap-2">
        {/* Owned Column */}
        <div className="flex flex-col items-center">
          <label
            htmlFor="editor-owned-qty"
            className="text-[11px] font-semibold uppercase tracking-wider text-muted-foreground mb-1"
          >
            Owned
          </label>
          <div className="flex items-center w-full">
            <button
              type="button"
              className="h-7 w-6 rounded-l border border-r-0 border-white/20 bg-[#141416] text-white text-xs font-semibold hover:bg-white/10 disabled:opacity-40"
              onClick={() => handleStep("owned", -1)}
              disabled={normalizeQty(ownedDraft) <= 0 || mutation.isPending}
              aria-label="Decrease owned"
            >
              −
            </button>
            <input
              id="editor-owned-qty"
              aria-label="Owned"
              type="text"
              inputMode="numeric"
              pattern="[0-9]*"
              className="h-7 w-full border border-white/20 bg-[#141416] text-white text-center text-xs font-medium focus-visible:outline-none focus-visible:ring-1 focus-visible:ring-blue-500"
              value={isLoading ? "…" : ownedDraft}
              onChange={(e) => setOwnedDraft(e.target.value)}
              onBlur={() => commitChanges()}
              onKeyDown={(e) => {
                if (e.key === "Enter") {
                  e.currentTarget.blur();
                }
              }}
            />
            <button
              type="button"
              className="h-7 w-6 rounded-r border border-l-0 border-white/20 bg-[#141416] text-white text-xs font-semibold hover:bg-white/10 disabled:opacity-40"
              onClick={() => handleStep("owned", 1)}
              disabled={mutation.isPending}
              aria-label="Increase owned"
            >
              +
            </button>
          </div>
        </div>

        {/* Proxy Column */}
        <div className="flex flex-col items-center">
          <label
            htmlFor="editor-proxy-qty"
            className="text-[11px] font-semibold uppercase tracking-wider text-muted-foreground mb-1"
          >
            Proxy
          </label>
          <div className="flex items-center w-full">
            <button
              type="button"
              className="h-7 w-6 rounded-l border border-r-0 border-white/20 bg-[#141416] text-white text-xs font-semibold hover:bg-white/10 disabled:opacity-40"
              onClick={() => handleStep("proxy", -1)}
              disabled={normalizeQty(proxyDraft) <= 0 || mutation.isPending}
              aria-label="Decrease proxy"
            >
              −
            </button>
            <input
              id="editor-proxy-qty"
              aria-label="Proxy"
              type="text"
              inputMode="numeric"
              pattern="[0-9]*"
              className="h-7 w-full border border-white/20 bg-[#141416] text-white text-center text-xs font-medium focus-visible:outline-none focus-visible:ring-1 focus-visible:ring-blue-500"
              value={isLoading ? "…" : proxyDraft}
              onChange={(e) => setProxyDraft(e.target.value)}
              onBlur={() => commitChanges()}
              onKeyDown={(e) => {
                if (e.key === "Enter") {
                  e.currentTarget.blur();
                }
              }}
            />
            <button
              type="button"
              className="h-7 w-6 rounded-r border border-l-0 border-white/20 bg-[#141416] text-white text-xs font-semibold hover:bg-white/10 disabled:opacity-40"
              onClick={() => handleStep("proxy", 1)}
              disabled={mutation.isPending}
              aria-label="Increase proxy"
            >
              +
            </button>
          </div>
        </div>

        {/* Wishlist Column */}
        <div className="flex flex-col items-center">
          <label
            htmlFor="editor-wishlist-qty"
            className="text-[11px] font-semibold uppercase tracking-wider text-muted-foreground mb-1"
          >
            Wishlist
          </label>
          <div className="flex items-center w-full">
            <button
              type="button"
              className="h-7 w-6 rounded-l border border-r-0 border-white/20 bg-[#141416] text-white text-xs font-semibold hover:bg-white/10 disabled:opacity-40"
              onClick={() => handleStep("wishlist", -1)}
              disabled={normalizeQty(wishlistDraft) <= 0 || mutation.isPending}
              aria-label="Decrease wishlist"
            >
              −
            </button>
            <input
              id="editor-wishlist-qty"
              aria-label="Wishlist"
              type="text"
              inputMode="numeric"
              pattern="[0-9]*"
              className="h-7 w-full border border-white/20 bg-[#141416] text-white text-center text-xs font-medium focus-visible:outline-none focus-visible:ring-1 focus-visible:ring-blue-500"
              value={isLoading ? "…" : wishlistDraft}
              onChange={(e) => setWishlistDraft(e.target.value)}
              onBlur={() => commitChanges()}
              onKeyDown={(e) => {
                if (e.key === "Enter") {
                  e.currentTarget.blur();
                }
              }}
            />
            <button
              type="button"
              className="h-7 w-6 rounded-r border border-l-0 border-white/20 bg-[#141416] text-white text-xs font-semibold hover:bg-white/10 disabled:opacity-40"
              onClick={() => handleStep("wishlist", 1)}
              disabled={mutation.isPending}
              aria-label="Increase wishlist"
            >
              +
            </button>
          </div>
        </div>
      </div>
    </div>
  );
}

