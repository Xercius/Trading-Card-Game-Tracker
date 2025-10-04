namespace api.Features.Cards.Dtos;

public sealed class CardListItemResponse
{
    public int CardId { get; set; }
    public required string Game { get; set; }
    public required string Name { get; set; }
    public required string CardType { get; set; }

    public PrimaryPrintingResponse? Primary { get; set; }
    public int PrintingsCount { get; set; }

    public sealed class PrimaryPrintingResponse
    {
        public int Id { get; set; }
        public required string Set { get; set; }
        public required string Number { get; set; }
        public required string Rarity { get; set; }
        public required string Style { get; set; }
        public string? ImageUrl { get; set; }
    }
}
