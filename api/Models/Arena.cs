using System.ComponentModel.DataAnnotations;

namespace api.Models;

public sealed class Arena
{
    [Key]
    public int Id { get; set; }

    public int GameId { get; set; }
    public Game Game { get; set; } = null!;

    [Required]
    [MaxLength(64)]
    public required string Name { get; set; }

    public ICollection<CardArena> Cards { get; set; } = new List<CardArena>();
}
