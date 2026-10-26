using System.ComponentModel.DataAnnotations;

namespace api.Models;

public sealed class CardType
{
    [Key]
    public int Id { get; set; }

    public int GameId { get; set; }
    public Game Game { get; set; } = null!;

    [Required]
    [MaxLength(128)]
    public required string Name { get; set; }

    [MaxLength(64)]
    public string? Value { get; set; }

    public ICollection<CardCardType> Cards { get; set; } = new List<CardCardType>();
}
