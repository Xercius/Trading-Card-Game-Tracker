namespace api.Models;

public class CardPriceHistory
{
    public int Id { get; set; }

    public int CardPrintingId { get; set; }

    public DateOnly CapturedAt { get; set; }

    public decimal Price { get; set; }

    public CardPrinting CardPrinting { get; set; } = default!;
}
