using api.Features.Collections.Dtos;
using api.Models;
using api.Shared;
using AutoMapper;

namespace api.Features.Collections.Mapping;

public sealed class CollectionsMappingProfile : Profile
{
    public CollectionsMappingProfile()
    {
        CreateMap<UserCard, UserCardItemResponse>()
            .ForCtorParam(nameof(UserCardItemResponse.CardPrintingId), opt => opt.MapFrom(uc => uc.CardPrintingId))
            .ForCtorParam(nameof(UserCardItemResponse.QuantityOwned), opt => opt.MapFrom(uc => uc.QuantityOwned))
            .ForCtorParam(nameof(UserCardItemResponse.QuantityWanted), opt => opt.MapFrom(uc => uc.QuantityWanted))
            .ForCtorParam(nameof(UserCardItemResponse.QuantityProxyOwned), opt => opt.MapFrom(uc => uc.QuantityProxyOwned))
            .ForCtorParam(nameof(UserCardItemResponse.Availability), opt => opt.MapFrom(CardAvailabilityHelper.AvailabilityExpression))
            .ForCtorParam(nameof(UserCardItemResponse.AvailabilityWithProxies), opt => opt.MapFrom(CardAvailabilityHelper.AvailabilityWithProxiesExpression))
            .ForCtorParam(nameof(UserCardItemResponse.CardId), opt => opt.MapFrom(uc => uc.CardPrinting.CardId))
            .ForCtorParam(nameof(UserCardItemResponse.CardName), opt => opt.MapFrom(uc => uc.CardPrinting.Card.Name))
            .ForCtorParam(nameof(UserCardItemResponse.Game), opt => opt.MapFrom(uc => uc.CardPrinting.Card.Game))
            .ForCtorParam(nameof(UserCardItemResponse.Set), opt => opt.MapFrom(uc => uc.CardPrinting.Set))
            .ForCtorParam(nameof(UserCardItemResponse.Number), opt => opt.MapFrom(uc => uc.CardPrinting.Number))
            .ForCtorParam(nameof(UserCardItemResponse.Rarity), opt => opt.MapFrom(uc => uc.CardPrinting.Rarity))
            .ForCtorParam(nameof(UserCardItemResponse.Style), opt => opt.MapFrom(uc => uc.CardPrinting.Style))
            .ForCtorParam(nameof(UserCardItemResponse.ImageUrl), opt => opt.MapFrom(uc => uc.CardPrinting.ImageUrl));
    }
}
