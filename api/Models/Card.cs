namespace api.Models
{
    public class Card
    {
        public int Id { get; set; }
        public required string Game { get; set; } // Magic, Lorcana, Star Wars Unlimited etc..
        public required string Name { get; set; } // Name of card
        public required string CardType { get; set; } // Unit, Instant, Sorcery, Upgrade, Enchantment etc..
        public string? Description { get; set; } // Optional rules text
        public string? DetailsJson { get; set; } // Source-specific payload
        public ICollection<CardPrinting> Printings { get; set; } = new List<CardPrinting>();
    }
}
