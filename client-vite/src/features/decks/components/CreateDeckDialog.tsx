import * as React from "react";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Badge } from "@/components/ui/badge";
import { Dialog, DialogContent } from "@/components/ui/dialog";
import { useCreateDeckMutation } from "../api";

export type CreateDeckDialogProps = {
  isOpen: boolean;
  onClose: () => void;
  game: string;
  onDeckCreated: (deckId: number) => void;
};

export function CreateDeckDialog({ isOpen, onClose, game, onDeckCreated }: CreateDeckDialogProps) {
  const isSwu = game.toLowerCase().includes("unlimited");
  const isSwccg = game.toLowerCase().includes("ccg");

  const [format, setFormat] = React.useState<string>(() => (isSwu ? "Premier" : "Standard"));
  const [side, setSide] = React.useState<string>(() => (isSwccg ? "Light" : ""));
  const [name, setName] = React.useState("");
  const [description, setDescription] = React.useState("");
  const [errorMsg, setErrorMsg] = React.useState<string | null>(null);

  const createMutation = useCreateDeckMutation();

  // Reset defaults when opening or game changes
  React.useEffect(() => {
    if (isOpen) {
      setFormat(isSwu ? "Premier" : "Standard");
      setSide(isSwccg ? "Light" : "");
      setName("");
      setDescription("");
      setErrorMsg(null);
    }
  }, [isOpen, isSwu, isSwccg]);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    const trimmedName = name.trim();
    if (!trimmedName) {
      setErrorMsg("Deck name is required.");
      return;
    }
    if (isSwccg && !side) {
      setErrorMsg("Please select a side (Light or Dark).");
      return;
    }
    if (!format) {
      setErrorMsg("Please select a format.");
      return;
    }

    try {
      setErrorMsg(null);
      const newDeck = await createMutation.mutateAsync({
        game,
        name: trimmedName,
        description: description.trim() || undefined,
        format,
        side: isSwccg ? side : undefined,
      });
      onDeckCreated(newDeck.id);
      onClose();
    } catch (err: unknown) {
      const msg = err instanceof Error ? err.message : "Failed to create deck.";
      setErrorMsg(msg);
    }
  };

  return (
    <Dialog open={isOpen} onOpenChange={(open) => !open && onClose()}>
      <DialogContent
        labelledBy="create-deck-dialog-title"
        className="max-w-xl rounded-xl border-zinc-800 bg-zinc-950 p-6 text-zinc-100 shadow-2xl space-y-5"
      >
        <div className="flex items-center justify-between border-b border-zinc-800 pb-3">
          <div>
            <h2 id="create-deck-dialog-title" className="text-xl font-bold text-white">
              Create New Deck
            </h2>
            <p className="text-xs text-zinc-400 mt-0.5">
              Select format and starting rules for {game}.
            </p>
          </div>
          <button
            type="button"
            onClick={onClose}
            aria-label="Close dialog"
            className="rounded-lg p-1.5 text-zinc-400 hover:text-white hover:bg-zinc-800 transition-colors"
          >
            <svg className="w-5 h-5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
              <path
                strokeLinecap="round"
                strokeLinejoin="round"
                strokeWidth={2}
                d="M6 18L18 6M6 6l12 12"
              />
            </svg>
          </button>
        </div>

        <form onSubmit={handleSubmit} className="space-y-4">
          {/* Format selection */}
          <div className="space-y-2">
            <label className="block text-xs font-semibold uppercase tracking-wider text-zinc-400">
              Format <span className="text-red-400">*</span>
            </label>

            {isSwu ? (
              <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
                <div
                  role="button"
                  tabIndex={0}
                  onClick={() => setFormat("Premier")}
                  onKeyDown={(e) => (e.key === "Enter" || e.key === " ") && setFormat("Premier")}
                  className={`p-3.5 rounded-lg border text-left cursor-pointer transition-all ${
                    format === "Premier"
                      ? "border-sky-500 bg-sky-950/40 text-white ring-1 ring-sky-500"
                      : "border-zinc-800 bg-zinc-900/60 text-zinc-300 hover:border-zinc-700"
                  }`}
                >
                  <div className="flex items-center justify-between">
                    <span className="font-semibold text-sm">Premier</span>
                    <Badge variant="outline" className="text-[10px] border-zinc-700 text-zinc-400">
                      Standard
                    </Badge>
                  </div>
                  <p className="mt-1 text-xs text-zinc-400 leading-relaxed">
                    1 Leader, 1 Base, 50+ card minimum. Max 3 copies per card.
                  </p>
                </div>

                <div
                  role="button"
                  tabIndex={0}
                  onClick={() => setFormat("Twin Suns")}
                  onKeyDown={(e) => (e.key === "Enter" || e.key === " ") && setFormat("Twin Suns")}
                  className={`p-3.5 rounded-lg border text-left cursor-pointer transition-all ${
                    format === "Twin Suns"
                      ? "border-sky-500 bg-sky-950/40 text-white ring-1 ring-sky-500"
                      : "border-zinc-800 bg-zinc-900/60 text-zinc-300 hover:border-zinc-700"
                  }`}
                >
                  <div className="flex items-center justify-between">
                    <span className="font-semibold text-sm">Twin Suns</span>
                    <Badge variant="outline" className="text-[10px] border-zinc-700 text-zinc-400">
                      Singleton
                    </Badge>
                  </div>
                  <p className="mt-1 text-xs text-zinc-400 leading-relaxed">
                    2 Leaders, 1 Base, 80+ card minimum. Max 1 copy per card.
                  </p>
                </div>
              </div>
            ) : isSwccg ? (
              <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
                <div
                  role="button"
                  tabIndex={0}
                  onClick={() => setFormat("Standard")}
                  onKeyDown={(e) => (e.key === "Enter" || e.key === " ") && setFormat("Standard")}
                  className={`p-3.5 rounded-lg border text-left cursor-pointer transition-all ${
                    format === "Standard"
                      ? "border-sky-500 bg-sky-950/40 text-white ring-1 ring-sky-500"
                      : "border-zinc-800 bg-zinc-900/60 text-zinc-300 hover:border-zinc-700"
                  }`}
                >
                  <div className="flex items-center justify-between">
                    <span className="font-semibold text-sm">Standard</span>
                    <Badge variant="outline" className="text-[10px] border-zinc-700 text-zinc-400">
                      All Sets
                    </Badge>
                  </div>
                  <p className="mt-1 text-xs text-zinc-400 leading-relaxed">
                    60 cards minimum (shields excluded). Supports all Decipher &amp; Virtual sets.
                  </p>
                </div>

                <div
                  role="button"
                  tabIndex={0}
                  onClick={() => setFormat("Decipher Only")}
                  onKeyDown={(e) =>
                    (e.key === "Enter" || e.key === " ") && setFormat("Decipher Only")
                  }
                  className={`p-3.5 rounded-lg border text-left cursor-pointer transition-all ${
                    format === "Decipher Only"
                      ? "border-sky-500 bg-sky-950/40 text-white ring-1 ring-sky-500"
                      : "border-zinc-800 bg-zinc-900/60 text-zinc-300 hover:border-zinc-700"
                  }`}
                >
                  <div className="flex items-center justify-between">
                    <span className="font-semibold text-sm">Decipher Only</span>
                    <Badge variant="outline" className="text-[10px] border-zinc-700 text-zinc-400">
                      Classic
                    </Badge>
                  </div>
                  <p className="mt-1 text-xs text-zinc-400 leading-relaxed">
                    60 cards minimum (shields excluded). Virtual sets are not allowed.
                  </p>
                </div>
              </div>
            ) : null}
          </div>

          {/* SWCCG Side Selection */}
          {isSwccg && (
            <div className="space-y-2">
              <label className="block text-xs font-semibold uppercase tracking-wider text-zinc-400">
                Side <span className="text-red-400">*</span>
              </label>
              <div className="grid grid-cols-2 gap-3">
                <button
                  type="button"
                  onClick={() => setSide("Light")}
                  className={`py-2.5 px-3 rounded-lg border text-center font-medium text-sm transition-all ${
                    side === "Light"
                      ? "border-amber-400 bg-amber-500/20 text-amber-200 ring-1 ring-amber-400"
                      : "border-zinc-800 bg-zinc-900 text-zinc-400 hover:border-zinc-700 hover:text-zinc-200"
                  }`}
                >
                  Light Side
                </button>
                <button
                  type="button"
                  onClick={() => setSide("Dark")}
                  className={`py-2.5 px-3 rounded-lg border text-center font-medium text-sm transition-all ${
                    side === "Dark"
                      ? "border-red-500 bg-red-950/40 text-red-200 ring-1 ring-red-500"
                      : "border-zinc-800 bg-zinc-900 text-zinc-400 hover:border-zinc-700 hover:text-zinc-200"
                  }`}
                >
                  Dark Side
                </button>
              </div>
            </div>
          )}

          {/* Deck Name */}
          <div className="space-y-1.5">
            <label
              htmlFor="deck-name-input"
              className="block text-xs font-semibold uppercase tracking-wider text-zinc-400"
            >
              Deck Name <span className="text-red-400">*</span>
            </label>
            <Input
              id="deck-name-input"
              type="text"
              placeholder="e.g. Luke Skywalker Control"
              value={name}
              onChange={(e) => setName(e.target.value)}
              className="bg-zinc-900 border-zinc-700 text-zinc-100 placeholder:text-zinc-600 focus-visible:ring-sky-500"
            />
          </div>

          {/* Deck Description */}
          <div className="space-y-1.5">
            <label
              htmlFor="deck-desc-input"
              className="block text-xs font-semibold uppercase tracking-wider text-zinc-400"
            >
              Description (Optional)
            </label>
            <textarea
              id="deck-desc-input"
              rows={2}
              placeholder="Strategy, key interactions, notes..."
              value={description}
              onChange={(e) => setDescription(e.target.value)}
              className="w-full rounded-md border border-zinc-700 bg-zinc-900 px-3 py-2 text-sm text-zinc-100 placeholder:text-zinc-600 shadow-inner focus:outline-none focus:ring-1 focus:ring-sky-500 resize-none"
            />
          </div>

          {errorMsg && (
            <div className="text-xs text-red-400 bg-red-950/50 border border-red-900/60 p-2 rounded">
              {errorMsg}
            </div>
          )}

          {/* Dialog Actions */}
          <div className="flex justify-end gap-3 pt-2">
            <Button
              type="button"
              variant="outline"
              size="sm"
              onClick={onClose}
              disabled={createMutation.isPending}
              className="border-zinc-700 bg-zinc-900 hover:bg-zinc-800 text-zinc-300"
            >
              Cancel
            </Button>
            <Button
              type="submit"
              size="sm"
              disabled={createMutation.isPending || !name.trim()}
              className="bg-sky-600 hover:bg-sky-500 text-white font-medium shadow-sm transition-colors"
            >
              {createMutation.isPending ? "Creating..." : "Create & Build Deck"}
            </Button>
          </div>
        </form>
      </DialogContent>
    </Dialog>
  );
}
