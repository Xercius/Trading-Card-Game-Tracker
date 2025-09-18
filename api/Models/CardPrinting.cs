namespace api.Models
{
    public class CardPrinting
    {
        public int Id { get; set; } // Primary Key
        public int CardId { get; set; } // Foreign Key
        public Card Card { get; set; } = null!; // Navigation property

        public string Set { get; set; } = null!; // Card set
        public string Number { get; set; } = null!; // Card's set number
        public string Rarity { get; set; } = null!; // Rarity of the card
        public string Style { get; set; } = null!; // Standard, Foil, Hyperspace, Showcase etc..
        public string ImageUrl { get; set; } // url to card image
    }
}
