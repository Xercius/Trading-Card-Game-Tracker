using System.ComponentModel.DataAnnotations;
namespace api.Models
{
    public class Deck
    {
        [Key] public int Id { get; set; }
        [Required] public int UserId { get; set; }
        public User? User { get; set; }
        [Required, MaxLength(120)] public string Name { get; set; } = "";
        public int? GameId { get; set; }
        public Game? GameNavigation { get; set; }
        public required string Game { get; set; }
        public string? Description { get; set; }
        [MaxLength(40)] public string? Format { get; set; }
        [MaxLength(20)] public string? Side { get; set; }

        public DateTimeOffset CreatedUtc { get; set; } = DateTimeOffset.UtcNow;
        public DateTimeOffset? UpdatedUtc { get; set; }

        public ICollection<DeckCard> Cards { get; set; } = new List<DeckCard>();
    }
}
