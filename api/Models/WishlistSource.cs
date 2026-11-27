using System.ComponentModel.DataAnnotations;

namespace api.Models;

/// <summary>
/// Tracks the source of a wishlist request (General want when <see cref="DeckId"/> is null,
/// or a specific deck when <see cref="DeckId"/> is set).
/// </summary>
public class WishlistSource
{
    [Key]
    public int Id { get; set; }

    [Required]
    public int CardPrintingId { get; set; }
    public CardPrinting CardPrinting { get; set; } = null!;

    public int? DeckId { get; set; }
    public Deck? Deck { get; set; }

    public int Quantity { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
