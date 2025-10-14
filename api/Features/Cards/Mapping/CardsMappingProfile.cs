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
        CreateMap<CardPrinting, CardPrintingResponse>();
        CreateMap<CardPrinting, CardListItemResponse.PrimaryPrintingResponse>();

        // Card list item mapping: transforms Card entity to a lightweight response for list views
        // Maps card ID to CardId property and counts associated printings
        // Primary property is intentionally ignored and populated separately in the controller
        CreateMap<Card, CardListItemResponse>()
            .ForMember(d => d.CardId, o => o.MapFrom(s => s.Id))
            .ForMember(d => d.Game, o => o.MapFrom(s => s.Game))
            .ForMember(d => d.Name, o => o.MapFrom(s => s.Name))
            .ForMember(d => d.CardType, o => o.MapFrom(s => s.CardType))
            .ForMember(d => d.PrintingsCount, o => o.MapFrom(s => s.Printings.Count))
            .ForMember(d => d.Primary, o => o.Ignore());

        // Card detail mapping: transforms Card entity to a complete response including all printings
        // Uses constructor mapping since CardDetailResponse is a record with a primary constructor
        CreateMap<Card, CardDetailResponse>()
            .ForCtorParam(nameof(CardDetailResponse.Id), o => o.MapFrom(s => s.Id))
            .ForCtorParam(nameof(CardDetailResponse.Name), o => o.MapFrom(s => s.Name))
            .ForCtorParam(nameof(CardDetailResponse.Game), o => o.MapFrom(s => s.Game))
            .ForCtorParam(nameof(CardDetailResponse.Printings), o => o.MapFrom(s => s.Printings));
    }
}
