import * as React from "react";
import { useGame } from "@/state/useGame";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import { Input } from "@/components/ui/input";
import { CardImageHoverPreview } from "@/components/ui/CardImageHoverPreview";
import { DeckConstructionModal } from "@/features/decks/components/DeckConstructionModal";
import { CreateDeckDialog } from "@/features/decks/components/CreateDeckDialog";
import { SWU_ASPECT_IMAGES } from "@/features/decks/utils/deckRules";
import {
  useDecksListQuery,
  useDeleteDeckMutation,
  type DeckListItem,
  type DeckFeaturedCard,
} from "@/features/decks/api";

function getDeckValidationStatus(deck: DeckListItem): { isLegal: boolean; label: string } {
  const isTwinSuns = deck.format?.toLowerCase() === "twin suns";
  const isSwu = (deck.game || "").toLowerCase().includes("unlimited");
  const isSwccg = (deck.game || "").toLowerCase().includes("ccg");

  if (isSwu) {
    const requiredLeaders = isTwinSuns ? 2 : 1;
    const minOther = isTwinSuns ? 80 : 50;
    const leaders = (deck.featuredCards ?? []).filter(
      (c) => c.cardType?.toLowerCase() === "leader"
    );
    const hasBase = Boolean(deck.baseCard);
    const hasLeaders = leaders.length === requiredLeaders;
    const hasEnoughCards = deck.cardCount >= minOther;

    if (hasLeaders && hasBase && hasEnoughCards) {
      return { isLegal: true, label: "✓ Legal" };
    }
    return { isLegal: false, label: "⚠️ Incomplete" };
  }

  if (isSwccg) {
    const hasSide = Boolean(deck.side);
    const hasEnoughCards = deck.cardCount >= 60;
    if (hasSide && hasEnoughCards) {
      return { isLegal: true, label: "✓ Legal" };
    }
    return { isLegal: false, label: "⚠️ Incomplete" };
  }

  return { isLegal: true, label: "✓ Ready" };
}

export default function DecksPage() {
  const { activeGame } = useGame();
  const [searchQuery, setSearchQuery] = React.useState("");
  const [activeDeckId, setActiveDeckId] = React.useState<number | null>(null);
  const [isModalOpen, setIsModalOpen] = React.useState(false);
  const [isCreateDialogOpen, setIsCreateDialogOpen] = React.useState(false);

  // Deck deletion dialog state
  const [deckToDelete, setDeckToDelete] = React.useState<DeckListItem | null>(null);

  const { data, isLoading, isError, error } = useDecksListQuery(
    activeGame,
    searchQuery.trim() || undefined,
    1,
    100
  );

  const deleteDeckMutation = useDeleteDeckMutation();

  const decks = data?.items ?? [];
  const total = data?.total ?? 0;

  const handleOpenDeck = (deckId: number) => {
    setActiveDeckId(deckId);
    setIsModalOpen(true);
  };

  const handleDeckCreated = (deckId: number) => {
    setActiveDeckId(deckId);
    setIsModalOpen(true);
  };

  const handleConfirmDelete = async () => {
    if (!deckToDelete) return;
    try {
      await deleteDeckMutation.mutateAsync(deckToDelete.id);
      setDeckToDelete(null);
    } catch (err) {
      console.error("Failed to delete deck:", err);
    }
  };

  return (
    <div className="space-y-6 p-4 sm:p-6 max-w-7xl mx-auto">
      {/* Page Header */}
      <div className="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-4 border-b border-zinc-800 pb-5">
        <div>
          <h1 className="text-2xl sm:text-3xl font-bold tracking-tight text-white">
            {activeGame} Decks
          </h1>
          <p className="mt-1 text-sm text-zinc-400">
            Build and manage your decks, review card lists, and track leader or objective cards.
          </p>
        </div>

        <div className="flex items-center gap-3">
          <Button
            onClick={() => setIsCreateDialogOpen(true)}
            className="bg-sky-600 hover:bg-sky-500 text-white font-medium shadow-sm transition-colors"
          >
            + Create Deck
          </Button>
        </div>
      </div>

      {/* Search Bar & Stats */}
      <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-3">
        <div className="relative w-full sm:w-80">
          <Input
            type="search"
            value={searchQuery}
            placeholder={`Search ${activeGame} decks...`}
            onChange={(e) => setSearchQuery(e.target.value)}
            className="bg-zinc-900 border-zinc-700 text-sm placeholder:text-zinc-500 focus-visible:ring-sky-500"
          />
        </div>

        <div className="text-xs text-zinc-500">
          Showing {decks.length} of {total} decks
        </div>
      </div>

      {/* Main Grid or Status States */}
      {isLoading ? (
        <div className="py-20 text-center text-sm text-zinc-400">Loading decks...</div>
      ) : isError ? (
        <div className="py-20 text-center text-sm text-red-400">
          Error loading decks: {error?.message}
        </div>
      ) : decks.length === 0 ? (
        <div className="py-20 text-center rounded-xl border border-dashed border-zinc-800 bg-zinc-900/30 p-8 space-y-4">
          <div className="mx-auto flex h-12 w-12 items-center justify-center rounded-full bg-zinc-800 text-zinc-400">
            <svg className="w-6 h-6" fill="none" viewBox="0 0 24 24" stroke="currentColor">
              <path
                strokeLinecap="round"
                strokeLinejoin="round"
                strokeWidth={2}
                d="M19 11H5m14 0a2 2 0 012 2v6a2 2 0 01-2 2H5a2 2 0 01-2-2v-6a2 2 0 012-2m14 0V9a2 2 0 00-2-2M5 11V9a2 2 0 012-2m0 0V5a2 2 0 012-2h6a2 2 0 012 2v2M7 7h10"
              />
            </svg>
          </div>
          <div>
            <h3 className="text-base font-semibold text-zinc-200">No decks found</h3>
            <p className="mt-1 text-sm text-zinc-500">
              {searchQuery.trim()
                ? `No decks matching "${searchQuery}".`
                : `You haven't created any ${activeGame} decks yet.`}
            </p>
          </div>
          <Button
            onClick={() => setIsCreateDialogOpen(true)}
            className="bg-sky-600 hover:bg-sky-500 text-white"
          >
            Create your first deck
          </Button>
        </div>
      ) : (
        <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
          {decks.map((deck) => {
            const isSwu = (deck.game || activeGame).toLowerCase().includes("unlimited");
            const isSwccg = (deck.game || activeGame).toLowerCase().includes("ccg");
            const status = getDeckValidationStatus(deck);

            // Collect featured items: Leaders/Objective + Base for SWU
            const featuredItems: DeckFeaturedCard[] = [...(deck.featuredCards ?? [])];
            if (
              isSwu &&
              deck.baseCard &&
              !featuredItems.some((f) => f.cardPrintingId === deck.baseCard?.cardPrintingId)
            ) {
              featuredItems.push(deck.baseCard);
            }

            return (
              <div
                key={deck.id}
                role="button"
                tabIndex={0}
                onClick={() => handleOpenDeck(deck.id)}
                onKeyDown={(e) => {
                  if (e.target === e.currentTarget && (e.key === "Enter" || e.key === " ")) {
                    e.preventDefault();
                    handleOpenDeck(deck.id);
                  }
                }}
                className="group relative flex flex-col justify-between rounded-xl border border-zinc-800/80 bg-zinc-900/50 p-5 hover:border-sky-500/60 hover:bg-zinc-900/90 hover:shadow-lg transition-all cursor-pointer text-left"
              >
                {/* Deck Card Header */}
                <div className="space-y-2">
                  <div className="flex items-start justify-between gap-3">
                    <h2 className="font-bold text-lg text-white group-hover:text-sky-300 transition-colors line-clamp-1">
                      {deck.name}
                    </h2>
                    <button
                      type="button"
                      aria-label={`Delete deck ${deck.name}`}
                      onClick={(e) => {
                        e.stopPropagation();
                        setDeckToDelete(deck);
                      }}
                      className="p-1.5 rounded-lg text-zinc-500 hover:text-red-400 hover:bg-red-950/40 opacity-70 group-hover:opacity-100 transition-all focus:outline-none"
                      title="Delete deck"
                    >
                      <svg
                        className="w-4 h-4"
                        fill="none"
                        viewBox="0 0 24 24"
                        stroke="currentColor"
                      >
                        <path
                          strokeLinecap="round"
                          strokeLinejoin="round"
                          strokeWidth={2}
                          d="M19 7l-.867 12.142A2 2 0 0116.138 21H7.862a2 2 0 01-1.995-1.858L5 7m5 4v6m4-6v6m1-10V4a1 1 0 00-1-1h-4a1 1 0 00-1 1v3M4 7h16"
                        />
                      </svg>
                    </button>
                  </div>

                  {/* Badges: Format, Side, SWU Aspects, Status */}
                  <div className="flex flex-wrap items-center gap-1.5 pt-1">
                    {deck.format && (
                      <Badge
                        variant="outline"
                        className="border-sky-500/40 bg-sky-950/40 text-sky-300 text-[11px] font-semibold"
                      >
                        {deck.format}
                      </Badge>
                    )}

                    {isSwccg && deck.side && (
                      <Badge
                        variant="outline"
                        className={
                          deck.side.toLowerCase() === "dark"
                            ? "border-red-500/40 bg-red-950/40 text-red-300 text-[11px] font-semibold"
                            : "border-amber-500/40 bg-amber-950/40 text-amber-300 text-[11px] font-semibold"
                        }
                      >
                        {deck.side} Side
                      </Badge>
                    )}

                    {/* Aspects for SWU */}
                    {isSwu && deck.aspects && deck.aspects.length > 0 && (
                      <div className="flex items-center gap-1 px-1.5 py-0.5 rounded-md bg-zinc-800/60 border border-zinc-700/50">
                        {deck.aspects.map((asp) => {
                          const img = SWU_ASPECT_IMAGES[asp];
                          if (!img) return null;
                          return (
                            <img
                              key={asp}
                              src={img}
                              alt={asp}
                              title={asp}
                              className="w-4 h-4 rounded-full object-contain"
                            />
                          );
                        })}
                      </div>
                    )}

                    {/* Legal / Incomplete Status Pill */}
                    <span
                      className={`text-[10px] font-semibold px-1.5 py-0.5 rounded border ${
                        status.isLegal
                          ? "text-emerald-400 bg-emerald-950/40 border-emerald-800/60"
                          : "text-amber-400 bg-amber-950/30 border-amber-800/50"
                      }`}
                    >
                      {status.label}
                    </span>
                  </div>

                  {deck.description && (
                    <p className="text-xs text-zinc-400 line-clamp-2 pt-1">{deck.description}</p>
                  )}
                </div>

                {/* Featured Leader(s), Base, or Objective */}
                {featuredItems.length > 0 && (
                  <div className="my-4 pt-3 border-t border-zinc-800/60">
                    <div className="text-[11px] font-semibold uppercase tracking-wider text-zinc-500 mb-2">
                      {isSwu ? "Leaders & Base" : isSwccg ? "Objective" : "Featured Cards"}
                    </div>
                    <div className="flex flex-wrap gap-2">
                      {featuredItems.map((card) => (
                        <div
                          key={card.cardPrintingId}
                          onClick={(e) => e.stopPropagation()}
                          className={
                            isSwu
                              ? "shrink-0"
                              : "flex items-center gap-2 p-1.5 rounded-lg bg-zinc-800/60 border border-zinc-700/60"
                          }
                        >
                          <CardImageHoverPreview
                            imageUrl={card.imageUrl}
                            cardName={card.cardName}
                            isHorizontal={card.isHorizontal}
                            thumbnailWidth={isSwu ? 144 : undefined}
                            thumbnailHeight={isSwu ? 104 : undefined}
                          />
                          {!isSwu && (
                            <div className="min-w-0 pr-1">
                              <div
                                className="text-xs font-medium text-zinc-200 truncate max-w-[140px]"
                                title={card.cardName}
                              >
                                {card.cardName}
                              </div>
                              <span className="text-[10px] text-sky-400">{card.cardType}</span>
                            </div>
                          )}
                        </div>
                      ))}
                    </div>
                  </div>
                )}

                {/* Deck Card Footer */}
                <div className="flex items-center justify-between text-xs text-zinc-500 pt-3 border-t border-zinc-800/60 mt-3">
                  <Badge
                    variant="outline"
                    className="border-zinc-700 bg-zinc-800/80 text-zinc-300 font-semibold text-xs"
                  >
                    {deck.cardCount} cards
                    {isSwccg &&
                    typeof deck.defensiveShieldCount === "number" &&
                    deck.defensiveShieldCount > 0
                      ? ` (+${deck.defensiveShieldCount} shields)`
                      : ""}
                  </Badge>

                  <span>
                    Updated {new Date(deck.updatedUtc || deck.createdUtc).toLocaleDateString()}
                  </span>
                </div>
              </div>
            );
          })}
        </div>
      )}

      {/* Create Deck Dialog */}
      <CreateDeckDialog
        isOpen={isCreateDialogOpen}
        onClose={() => setIsCreateDialogOpen(false)}
        game={activeGame}
        onDeckCreated={handleDeckCreated}
      />

      {/* Delete Confirmation Modal */}
      {deckToDelete && (
        <div
          role="dialog"
          aria-modal="true"
          aria-labelledby="confirm-delete-title"
          className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-black/75 backdrop-blur-sm"
        >
          <div
            className="w-full max-w-md rounded-xl border border-zinc-800 bg-zinc-900 p-6 text-zinc-100 shadow-2xl space-y-4"
            onClick={(e) => e.stopPropagation()}
          >
            <h3 id="confirm-delete-title" className="text-lg font-bold text-white">
              Delete Deck?
            </h3>
            <p className="text-sm text-zinc-400">
              Are you sure you want to delete{" "}
              <strong className="text-zinc-200">"{deckToDelete.name}"</strong>? This action cannot
              be undone.
            </p>
            <div className="flex justify-end gap-3 pt-2">
              <Button
                variant="outline"
                size="sm"
                onClick={() => setDeckToDelete(null)}
                disabled={deleteDeckMutation.isPending}
                className="border-zinc-700 bg-zinc-800 hover:bg-zinc-700 text-zinc-200"
              >
                Cancel
              </Button>
              <Button
                size="sm"
                onClick={handleConfirmDelete}
                disabled={deleteDeckMutation.isPending}
                className="bg-red-600 hover:bg-red-500 text-white"
              >
                {deleteDeckMutation.isPending ? "Deleting..." : "Delete Deck"}
              </Button>
            </div>
          </div>
        </div>
      )}

      {/* Deck Construction Modal */}
      {isModalOpen && (
        <DeckConstructionModal
          deckId={activeDeckId}
          isOpen={isModalOpen}
          onClose={() => setIsModalOpen(false)}
          game={activeGame}
        />
      )}
    </div>
  );
}
