using api.Features.Cards.Dtos;
using api.Models;
using AutoMapper;

namespace api.Features.Cards.Mapping;

/// <summary>
/// AutoMapper profile for the Cards feature that defines object-to-object mappings
/// between domain entities (Card, CardPrinting) and their corresponding DTOs.
/// 
/// This profile is automatically discovered and registered by AutoMapper during application startup.
/// It transforms EF Core entities into API response objects, ensuring a clean separation between
/// the persistence layer and the API contract.
/// </summary>
public sealed class CardsMappingProfile : Profile
{
    /// <summary>
    /// Configures all AutoMapper mappings for the Cards feature.
    /// 
    /// Defines mappings from domain entities to response DTOs:
    /// - CardPrinting → CardPrintingResponse: Simple property mapping for printing details
    /// - CardPrinting → PrimaryPrintingResponse: Subset mapping for card list views
    /// - Card → CardListItemResponse: Summary view with printing count, Primary property handled separately
    /// - Card → CardDetailResponse: Full view with nested printing collection
    /// 
    /// Side effects: None. This only configures mapping rules; actual transformations occur when Map() is called.
    /// </summary>
    public CardsMappingProfile()
    {
        // Direct property-to-property mapping for card printing details
        CreateMap<CardPrinting, CardPrintingResponse>()
            .ForCtorParam(nameof(CardPrintingResponse.Set), o => o.MapFrom(s =>
                s.CatalogSet == null ? s.Set : s.CatalogSet.Name ?? s.CatalogSet.Code))
            .ForCtorParam(nameof(CardPrintingResponse.Rarity), o => o.MapFrom(s =>
                s.RarityLookup == null ? s.Rarity : s.RarityLookup.Name))
            .ForCtorParam(nameof(CardPrintingResponse.Style), o => o.MapFrom(s =>
                s.Treatment == null ? "Standard" : s.Treatment.Name))
            .ForCtorParam(nameof(CardPrintingResponse.SetCode), o => o.MapFrom(s => s.CatalogSet == null ? null : s.CatalogSet.Code))
            .ForCtorParam(nameof(CardPrintingResponse.ReleaseDate), o => o.MapFrom(s =>
                s.CatalogSet == null ? s.ReleasedAt : s.CatalogSet.ReleasedAt))
            .ForCtorParam(nameof(CardPrintingResponse.BackImageUrl), o => o.MapFrom(s => s.BackImageUrl))
            .ForCtorParam(nameof(CardPrintingResponse.ImageOrientation), o => o.MapFrom(s =>
                s.ArtFrontHorizontal == true ? "horizontal" : s.ArtFrontHorizontal == false ? "vertical" : null))
            .ForCtorParam(nameof(CardPrintingResponse.BackImageOrientation), o => o.MapFrom(s =>
                s.ArtBackHorizontal == true ? "horizontal" : s.ArtBackHorizontal == false ? "vertical" : null))
            .ForCtorParam(nameof(CardPrintingResponse.Artist), o => o.MapFrom(s => s.Artist));
        CreateMap<CardPrinting, CardListItemResponse.PrimaryPrintingResponse>()
            .ForMember(d => d.Set, o => o.MapFrom(s =>
                s.CatalogSet == null ? s.Set : s.CatalogSet.Name ?? s.CatalogSet.Code))
            .ForMember(d => d.SetCode, o => o.MapFrom(s => s.CatalogSet == null ? null : s.CatalogSet.Code))
            .ForMember(d => d.Rarity, o => o.MapFrom(s =>
                s.RarityLookup == null ? s.Rarity : s.RarityLookup.Name))
            .ForMember(d => d.Style, o => o.MapFrom(s =>
                s.Treatment == null ? "Standard" : s.Treatment.Name))
            .ForMember(d => d.ReleaseDate, o => o.MapFrom(s =>
                s.CatalogSet == null ? s.ReleasedAt : s.CatalogSet.ReleasedAt));

        // Card list item mapping: transforms Card entity to a lightweight response for list views
        // Maps card ID to CardId property and counts associated printings
        // Primary property is intentionally ignored and populated separately in the controller
        CreateMap<Card, CardListItemResponse>()
            .ForMember(d => d.CardId, o => o.MapFrom(s => s.Id))
            .ForMember(d => d.Game, o => o.MapFrom(s => s.Game == null ? string.Empty : s.Game.Name))
            .ForMember(d => d.Name, o => o.MapFrom(s => s.Name))
            .ForMember(d => d.CardType, o => o.MapFrom(s =>
                s.CardTypes.Where(type => type.SourceRole == "Type")
                    .Select(type => type.CardType.Name).FirstOrDefault() ?? string.Empty))
            .ForMember(d => d.CardTypes, o => o.MapFrom(s =>
                s.CardTypes.Select(type => type.CardType.Name).Distinct().OrderBy(name => name).ToList()))
            .ForMember(d => d.Aspects, o => o.MapFrom(s => s.CardAspects
                .OrderBy(aspect => aspect.Aspect.Name)
                .Select(aspect => new CardAspectResponse(aspect.Aspect.Name, aspect.Count)).ToList()))
            .ForMember(d => d.PrintingsCount, o => o.MapFrom(s => s.Printings.Count))
            .ForMember(d => d.Primary, o => o.Ignore());

        // Card detail mapping: transforms Card entity to a complete response including all printings
        // Uses constructor mapping since CardDetailResponse is a record with a primary constructor
        CreateMap<Card, CardDetailResponse>()
            .ForCtorParam(nameof(CardDetailResponse.Id), o => o.MapFrom(s => s.Id))
            .ForCtorParam(nameof(CardDetailResponse.Name), o => o.MapFrom(s => s.Name))
            .ForCtorParam(nameof(CardDetailResponse.Game), o => o.MapFrom(s =>
                s.Game == null ? string.Empty : s.Game.Name))
            .ForCtorParam(nameof(CardDetailResponse.CardType), o => o.MapFrom(s =>
                s.CardTypes.Where(type => type.SourceRole == "Type")
                    .Select(type => type.CardType.Name).FirstOrDefault() ?? string.Empty))
            .ForCtorParam(nameof(CardDetailResponse.Text), o => o.MapFrom(s => s.Text))
            .ForCtorParam(nameof(CardDetailResponse.Printings), o => o.MapFrom(s => s.Printings))
            .ForCtorParam(nameof(CardDetailResponse.CardTypes), o => o.MapFrom(s =>
                s.CardTypes.Select(type => type.CardType.Name).Distinct().OrderBy(name => name).ToList()))
            .ForCtorParam(nameof(CardDetailResponse.Aspects), o => o.MapFrom(s => s.CardAspects
                .OrderBy(aspect => aspect.Aspect.Name)
                .Select(aspect => new CardAspectResponse(aspect.Aspect.Name, aspect.Count)).ToList()))
            .ForCtorParam(nameof(CardDetailResponse.CardType2), o => o.MapFrom(s =>
                s.CardTypes.Where(type => type.SourceRole == "Type2").Select(type => type.CardType.Name).FirstOrDefault()))
            .ForCtorParam(nameof(CardDetailResponse.Arena), o => o.MapFrom(s =>
                string.Join(", ", s.CardArenas.Select(a => a.Arena.Name).Distinct().OrderBy(n => n))))
            .ForCtorParam(nameof(CardDetailResponse.Traits), o => o.MapFrom(s =>
                s.CardTraits.Select(trait => trait.Trait.Name).Distinct().OrderBy(name => name).ToList()))
            .ForCtorParam(nameof(CardDetailResponse.TextStyled), o => o.MapFrom(s => s.TextStyled))
            .ForCtorParam(nameof(CardDetailResponse.EpicAction), o => o.MapFrom(s => s.EpicAction))
            .ForCtorParam(nameof(CardDetailResponse.EpicActionStyled), o => o.MapFrom(s => s.EpicActionStyled))
            .ForCtorParam(nameof(CardDetailResponse.DeployBox), o => o.MapFrom(s => s.DeployBox))
            .ForCtorParam(nameof(CardDetailResponse.DeployBoxStyled), o => o.MapFrom(s => s.DeployBoxStyled))
            .ForCtorParam(nameof(CardDetailResponse.Cost), o => o.MapFrom(s => s.Cost))
            .ForCtorParam(nameof(CardDetailResponse.Hp), o => o.MapFrom(s => s.Hp))
            .ForCtorParam(nameof(CardDetailResponse.Power), o => o.MapFrom(s => s.Power))
            .ForCtorParam(nameof(CardDetailResponse.UpgradeHp), o => o.MapFrom(s => s.UpgradeHp))
            .ForCtorParam(nameof(CardDetailResponse.UpgradePower), o => o.MapFrom(s => s.UpgradePower));
    }
}
