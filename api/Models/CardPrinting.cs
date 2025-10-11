namespace api.Models
{
    public class CardPrinting
    {
        public int Id { get; set; } // Primary Key
        public int CardId { get; set; } // Foreign Key
        public Card Card { get; set; } = null!; // Navigation property
        public required string Set { get; set; } // Card set
        public required string Number { get; set; } // Card's set number
        public required string Rarity { get; set; } // Rarity of the card
        public required string Style { get; set; } // Standard, Foil, Hyperspace, Showcase etc..
        public string? ImageUrl { get; set; } // url to card image
        public string? DetailsJson { get; set; } // Source-specific payload
        public ICollection<DeckCard> DeckCards { get; set; } = new List<DeckCard>();
    }
}
