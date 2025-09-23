using System.ComponentModel.DataAnnotations;
namespace api.Models
{
    public class Deck
    {
        [Key] public int Id { get; set; }
        [Required] public int UserId { get; set; }
        public User? User { get; set; }
        [Required, MaxLength(120)] public string Name { get; set; } = "";
        public string? Game { get; set; }
        public string? Description { get; set; }

        public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedUtc { get; set; }

        public ICollection<DeckCard> Cards { get; set; } = new List<DeckCard>();
    }
}
