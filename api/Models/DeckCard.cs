namespace api.Models
{
    public class DeckCard
    {
        public int Id { get; set; }

        public int DeckId { get; set; }
        public Deck Deck { get; set; } = null!;

        public int CardPrintingId { get; set; }
        public CardPrinting CardPrinting { get; set; } = null!;

        public int QuantityInDeck { get; set; }   // copies actually in deck
        public int QuantityIdea { get; set; }     // ideas to try
        public int QuantityAcquire { get; set; }  // want to acquire for this deck
    }
}