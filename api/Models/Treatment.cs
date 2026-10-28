using System.ComponentModel.DataAnnotations;

namespace api.Models;

public sealed class Treatment
{
    [Key]
    public int Id { get; set; }

    public int GameId { get; set; }
    public Game Game { get; set; } = null!;

    [Required]
    [MaxLength(128)]
    public required string Name { get; set; }

    public string NameNormalized { get; private set; } = null!;

    public string? IconUrl { get; set; }

    public ICollection<CardPrinting> CardPrintings { get; set; } = new List<CardPrinting>();
}
