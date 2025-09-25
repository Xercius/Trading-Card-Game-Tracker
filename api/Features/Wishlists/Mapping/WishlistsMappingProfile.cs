using AutoMapper;
using api.Features.Wishlists.Dtos;
using api.Models;

namespace api.Features.Wishlists.Mapping;

public sealed class WishlistsMappingProfile : Profile
{
    public WishlistsMappingProfile()
    {
        CreateMap<UserCard, WishlistItemResponse>()
            .ForCtorParam(nameof(WishlistItemResponse.CardPrintingId), opt => opt.MapFrom(uc => uc.CardPrintingId))
            .ForCtorParam(nameof(WishlistItemResponse.QuantityWanted), opt => opt.MapFrom(uc => uc.QuantityWanted))
            .ForCtorParam(nameof(WishlistItemResponse.CardId), opt => opt.MapFrom(uc => uc.CardPrinting.CardId))
            .ForCtorParam(nameof(WishlistItemResponse.CardName), opt => opt.MapFrom(uc => uc.CardPrinting.Card.Name))
            .ForCtorParam(nameof(WishlistItemResponse.Game), opt => opt.MapFrom(uc => uc.CardPrinting.Card.Game))
            .ForCtorParam(nameof(WishlistItemResponse.Set), opt => opt.MapFrom(uc => uc.CardPrinting.Set))
            .ForCtorParam(nameof(WishlistItemResponse.Number), opt => opt.MapFrom(uc => uc.CardPrinting.Number))
            .ForCtorParam(nameof(WishlistItemResponse.Rarity), opt => opt.MapFrom(uc => uc.CardPrinting.Rarity))
            .ForCtorParam(nameof(WishlistItemResponse.Style), opt => opt.MapFrom(uc => uc.CardPrinting.Style))
            .ForCtorParam(nameof(WishlistItemResponse.ImageUrl), opt => opt.MapFrom(uc => uc.CardPrinting.ImageUrl));
    }
}
