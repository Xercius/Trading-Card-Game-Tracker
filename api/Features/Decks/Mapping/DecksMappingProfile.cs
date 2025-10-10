using AutoMapper;
using api.Features.Decks.Dtos;
using api.Models;

namespace api.Features.Decks.Mapping;

public sealed class DecksMappingProfile : Profile
{
    public DecksMappingProfile()
    {
        CreateMap<Deck, DeckResponse>();

        CreateMap<CreateDeckRequest, Deck>()
            .ForMember(d => d.Game, opt => opt.MapFrom(s => s.Game.Trim()))
            .ForMember(d => d.Name, opt => opt.MapFrom(s => s.Name.Trim()))
            .ForMember(d => d.Description, opt => opt.MapFrom(s => s.Description));

        CreateMap<UpdateDeckRequest, Deck>()
            .ForMember(d => d.Game, opt => opt.MapFrom(s => s.Game.Trim()))
            .ForMember(d => d.Name, opt => opt.MapFrom(s => s.Name.Trim()))
            .ForMember(d => d.Description, opt => opt.MapFrom(s => s.Description));

        CreateMap<DeckCard, DeckCardItemResponse>()
            .ForCtorParam(nameof(DeckCardItemResponse.CardPrintingId), opt => opt.MapFrom(dc => dc.CardPrintingId))
            .ForCtorParam(nameof(DeckCardItemResponse.QuantityInDeck), opt => opt.MapFrom(dc => dc.QuantityInDeck))
            .ForCtorParam(nameof(DeckCardItemResponse.QuantityIdea), opt => opt.MapFrom(dc => dc.QuantityIdea))
            .ForCtorParam(nameof(DeckCardItemResponse.QuantityAcquire), opt => opt.MapFrom(dc => dc.QuantityAcquire))
            .ForCtorParam(nameof(DeckCardItemResponse.QuantityProxy), opt => opt.MapFrom(dc => dc.QuantityProxy))
            .ForCtorParam(nameof(DeckCardItemResponse.CardId), opt => opt.MapFrom(dc => dc.CardPrinting != null ? dc.CardPrinting.CardId : 0))
            .ForCtorParam(nameof(DeckCardItemResponse.CardName), opt => opt.MapFrom(dc => dc.CardPrinting != null && dc.CardPrinting.Card != null ? dc.CardPrinting.Card.Name : string.Empty))
            .ForCtorParam(nameof(DeckCardItemResponse.Game), opt => opt.MapFrom(dc => dc.CardPrinting != null && dc.CardPrinting.Card != null ? dc.CardPrinting.Card.Game : string.Empty))
            .ForCtorParam(nameof(DeckCardItemResponse.Set), opt => opt.MapFrom(dc => dc.CardPrinting != null ? dc.CardPrinting.Set : string.Empty))
            .ForCtorParam(nameof(DeckCardItemResponse.Number), opt => opt.MapFrom(dc => dc.CardPrinting != null ? dc.CardPrinting.Number : string.Empty))
            .ForCtorParam(nameof(DeckCardItemResponse.Rarity), opt => opt.MapFrom(dc => dc.CardPrinting != null ? dc.CardPrinting.Rarity : string.Empty))
            .ForCtorParam(nameof(DeckCardItemResponse.Style), opt => opt.MapFrom(dc => dc.CardPrinting != null ? dc.CardPrinting.Style : string.Empty))
            .ForCtorParam(nameof(DeckCardItemResponse.ImageUrl), opt => opt.MapFrom(dc => dc.CardPrinting != null ? dc.CardPrinting.ImageUrl : null));
    }
}
