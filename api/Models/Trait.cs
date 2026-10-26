using System.ComponentModel.DataAnnotations;

namespace api.Models;

public sealed class Trait
{
    [Key]
    public int Id { get; set; }

    public int GameId { get; set; }
    public Game Game { get; set; } = null!;

    [Required]
    [MaxLength(128)]
    public required string Name { get; set; }

    public ICollection<CardTrait> Cards { get; set; } = new List<CardTrait>();
}
