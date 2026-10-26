using System.ComponentModel.DataAnnotations;

namespace api.Models;

public sealed class Rarity
{
    [Key]
    public int Id { get; set; }

    public int GameId { get; set; }
    public Game Game { get; set; } = null!;

    [Required]
    [MaxLength(32)]
    public required string Name { get; set; }

    [MaxLength(1)]
    public string? Character { get; set; }

    [MaxLength(32)]
    public string? Color { get; set; }
}
