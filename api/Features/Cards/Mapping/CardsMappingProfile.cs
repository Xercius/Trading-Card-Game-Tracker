using api.Features.Cards.Dtos;
using api.Models;
using AutoMapper;

namespace api.Features.Cards.Mapping;

public sealed class CardsMappingProfile : Profile
{
    public CardsMappingProfile()
    {
        CreateMap<CardPrinting, CardPrintingResponse>();
        CreateMap<CardPrinting, CardListItemResponse.PrimaryPrintingResponse>();

        CreateMap<Card, CardListItemResponse>()
            .ForMember(d => d.CardId, o => o.MapFrom(s => s.Id))
            .ForMember(d => d.Game, o => o.MapFrom(s => s.Game))
            .ForMember(d => d.Name, o => o.MapFrom(s => s.Name))
            .ForMember(d => d.CardType, o => o.MapFrom(s => s.CardType))
            .ForMember(d => d.PrintingsCount, o => o.MapFrom(s => s.Printings.Count))
            .ForMember(d => d.Primary, o => o.Ignore());

        CreateMap<Card, CardDetailResponse>()
            .ForCtorParam(nameof(CardDetailResponse.CardId), o => o.MapFrom(s => s.Id))
            .ForCtorParam(nameof(CardDetailResponse.Name), o => o.MapFrom(s => s.Name))
            .ForCtorParam(nameof(CardDetailResponse.Game), o => o.MapFrom(s => s.Game))
            .ForCtorParam(nameof(CardDetailResponse.Printings), o => o.MapFrom(s => s.Printings));
    }
}
