namespace api.Models
{
    public class CardPrinting
    {
        public int Id { get; set; } // Primary Key
        public int CardId { get; set; } // Foreign Key
        public Card Card { get; set; } = null!; // Navigation property
        public required string Set { get; set; } // Card set
        public required string Number { get; set; } // Card's set number
        public required string Rarity { get; set; } // Rarity of the card
        public int? RarityId { get; set; }
        public Rarity? RarityLookup { get; set; }
        public int? TreatmentId { get; set; }
        public Treatment? Treatment { get; set; }
        public string? ImageUrl { get; set; } // url to card image
        public string? BackImageUrl { get; set; }
        public bool? ArtFrontHorizontal { get; set; }
        public bool? ArtBackHorizontal { get; set; }
        public bool IsFoil { get; set; }
        public int? VariantOfId { get; set; }
        public CardPrinting? VariantOf { get; set; }
        public int? ReprintOfId { get; set; }
        public CardPrinting? ReprintOf { get; set; }
        public string? DetailsJson { get; set; } // Source-specific payload
        public DateOnly? ReleasedAt { get; set; } // Release date when supplied by the source
        public int? SetId { get; set; }
        public Set? CatalogSet { get; set; }
        public int? StrapiId { get; set; }
        public string? CardUid { get; set; }
        public string? Artist { get; set; }
        public DateTimeOffset? ApiCreatedAt { get; set; }
        public DateTimeOffset? ApiUpdatedAt { get; set; }
        public DateTimeOffset? ApiPublishedAt { get; set; }
        public DateTimeOffset? LastSyncedAt { get; set; }
        public ICollection<DeckCard> DeckCards { get; set; } = new List<DeckCard>();
    }
}
