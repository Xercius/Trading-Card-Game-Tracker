using System;
using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace api.Models;

[Index(nameof(CardPrintingId), nameof(CapturedAt), IsUnique = true)]
public class CardPriceHistory
{
    [Key]
    public int Id { get; set; }

    public int CardPrintingId { get; set; }

    public DateOnly CapturedAt { get; set; }

    public decimal Price { get; set; }

    public CardPrinting CardPrinting { get; set; } = default!;
}
