using System.ComponentModel.DataAnnotations;

namespace api.Models;

public sealed class Aspect
{
    [Key]
    public int Id { get; set; }

    public int GameId { get; set; }
    public Game Game { get; set; } = null!;

    [Required]
    [MaxLength(64)]
    public required string Name { get; set; }

    [MaxLength(32)]
    public string? Color { get; set; }

    public ICollection<CardAspect> Cards { get; set; } = new List<CardAspect>();
}
