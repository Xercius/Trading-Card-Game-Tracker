namespace api.Models
{
    public class Deck
    {
        public int Id { get; set; }
        public int UserId { get; set; }                  // owner
        public string Game { get; set; } = string.Empty; // must match Card.Game
        public string Name { get; set; } = string.Empty; // per-user deck name
        public string? Description { get; set; }

        public User User { get; set; } = null!;
        public ICollection<DeckCard> Cards { get; set; } = new List<DeckCard>();
    }
}
