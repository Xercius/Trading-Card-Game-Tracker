using AutoMapper;
using api.Features.Cards.Dtos;
using api.Models;

namespace api.Features.Cards.Mapping;

public sealed class CardsMappingProfile : Profile
{
    public CardsMappingProfile()
    {
        CreateMap<CardPrinting, CardPrintingResponse>();

        CreateMap<Card, CardListItemResponse>()
            .ForCtorParam(nameof(CardListItemResponse.CardId), opt => opt.MapFrom(c => c.Id));

        CreateMap<Card, CardDetailResponse>()
            .ForCtorParam(nameof(CardDetailResponse.CardId), opt => opt.MapFrom(c => c.Id))
            .ForCtorParam(nameof(CardDetailResponse.Printings), opt => opt.MapFrom(c => c.Printings));
    }
}
