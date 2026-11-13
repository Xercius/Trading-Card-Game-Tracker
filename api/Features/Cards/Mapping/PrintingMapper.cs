using api.Features.Cards.Dtos;
using api.Models;

namespace api.Features.Cards.Mapping;

/// <summary>
/// Shared projection helpers for mapping <see cref="CardPrinting"/> entities to <see cref="PrintingDto"/>.
/// </summary>
internal static class PrintingMapper
{
    internal const string PlaceholderCardImage = "/images/placeholders/card-3x4.png";

    internal static string? GetOrientation(bool? horizontal)
        => horizontal switch { true => "horizontal", false => "vertical", _ => null };

    internal static DateOnly? GetReleaseDate(CardPrinting printing)
        => printing.CatalogSet?.ReleasedAt
            ?? (printing.ApiPublishedAt is { } publishedAt
                ? DateOnly.FromDateTime(publishedAt.UtcDateTime)
                : null);

    internal static PrintingDto ToPrintingDto(CardPrinting printing)
        => new(
            printing.Id,
            printing.CatalogSet == null ? string.Empty : printing.CatalogSet.Name ?? printing.CatalogSet.Code,
            printing.CatalogSet?.Code,
            printing.Number,
            printing.RarityLookup?.Name ?? string.Empty,
            string.IsNullOrWhiteSpace(printing.ImageUrl) ? PlaceholderCardImage : printing.ImageUrl,
            printing.CardId,
            printing.Card.Name,
            printing.Card.Game?.Name ?? string.Empty)
        {
            Style = printing.Treatment?.Name ?? "Standard",
            Cost = printing.Card.Cost,
            Subtitle = printing.Card.Subtitle,
            Destiny = printing.Card.Destiny,
            ReleaseDate = GetReleaseDate(printing),
            SetLogoUrl = printing.CatalogSet?.LogoUrl,
            BackImageUrl = printing.BackImageUrl,
            ImageOrientation = GetOrientation(printing.ArtFrontHorizontal),
            BackImageOrientation = GetOrientation(printing.ArtBackHorizontal),
            CardTypes = printing.Card.CardTypes.Select(type => type.CardType.Name)
                .Distinct().OrderBy(name => name).ToList(),
            Aspects = printing.Card.CardAspects.OrderBy(aspect => aspect.Aspect.Name)
                .Select(aspect => new CardAspectResponse(aspect.Aspect.Name, aspect.Count)).ToList()
        };
}
