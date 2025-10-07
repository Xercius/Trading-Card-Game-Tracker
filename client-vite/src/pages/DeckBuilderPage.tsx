import { useParams } from "react-router-dom";
import DeckBuilderFeature from "@/features/decks/DeckBuilderPage";

export default function DeckBuilderPage() {
  const params = useParams<{ deckId: string }>();
  const deckIdParam = params.deckId;
  const parsed = deckIdParam ? Number(deckIdParam) : NaN;

  if (!deckIdParam || !Number.isFinite(parsed) || parsed <= 0) {
    return <div className="p-4">Deck not found.</div>;
  }

  return <DeckBuilderFeature deckId={Math.trunc(parsed)} />;
}
