using System.ComponentModel.DataAnnotations;

namespace api.Models;

public sealed class Game
{
    [Key]
    public int Id { get; set; }

    [Required]
    [MaxLength(128)]
    public required string Name { get; set; }

    public string NameNormalized { get; private set; } = null!;

    public ICollection<Set> Sets { get; set; } = new List<Set>();
    public ICollection<CardType> CardTypes { get; set; } = new List<CardType>();
    public ICollection<Trait> Traits { get; set; } = new List<Trait>();
    public ICollection<Keyword> Keywords { get; set; } = new List<Keyword>();
    public ICollection<Arena> Arenas { get; set; } = new List<Arena>();
    public ICollection<Aspect> Aspects { get; set; } = new List<Aspect>();
    public ICollection<Rarity> Rarities { get; set; } = new List<Rarity>();
    public ICollection<Treatment> Treatments { get; set; } = new List<Treatment>();
}
