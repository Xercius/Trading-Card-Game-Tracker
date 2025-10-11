using api.Features.Wishlists.Dtos;
using FluentValidation;

namespace api.Features.Wishlists.Validation;

public sealed class UpsertWishlistValidator : AbstractValidator<UpsertWishlistRequest>
{
    public UpsertWishlistValidator()
    {
        RuleFor(x => x.CardPrintingId).GreaterThan(0);
        RuleFor(x => x.QuantityWanted).GreaterThanOrEqualTo(0);
    }
}
