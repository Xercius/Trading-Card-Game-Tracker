using System.ComponentModel.DataAnnotations;

namespace api.Models;

public sealed class SubType
{
    [Key]
    public int Id { get; set; }

    public int GameId { get; set; }
    public Game Game { get; set; } = null!;

    [Required]
    [MaxLength(128)]
    public required string Name { get; set; }

    public ICollection<CardSubType> Cards { get; set; } = new List<CardSubType>();
}

