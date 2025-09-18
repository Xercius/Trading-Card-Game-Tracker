namespace api.Models
{
    public class Card
    {
        public int Id { get; set; }
        public string Game { get; set; } = null!; // Magic, Lorcana, Star Wars Unlimited etc..
        public string Name { get; set; } = null!; // Name of card
        public string CardType { get; set; } = null!; // Unit, Instant, Sorcery, Upgrade, Enchatment etc..
        public string Description { get; set; } // Optional rules text
        public ICollection<CardPrinting> Printings { get; set; }
    }
}
