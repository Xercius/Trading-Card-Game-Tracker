using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace api.Models
{
    public class DeckCard
    {
        [Key] public int Id { get; set; }

        [Required] public int DeckId { get; set; }
        public Deck? Deck { get; set; }

        [Required] public int CardPrintingId { get; set; }
        public CardPrinting? CardPrinting { get; set; }

        // How many copies of this printing are in the deck, ideas for the deck, proxies in the deck and how many need to be acquired to be in the deck
        public int QuantityInDeck { get; set; } = 0;
        public int QuantityIdea { get; set; } = 0;
        public int QuantityProxy { get; set; } = 0;
        public int QuantityAcquire { get; set; } = 0;
    }
}