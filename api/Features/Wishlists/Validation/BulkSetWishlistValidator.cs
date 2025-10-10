using api.Features.Wishlists.Dtos;
using FluentValidation;

namespace api.Features.Wishlists.Validation;

public sealed class BulkSetWishlistValidator : AbstractValidator<BulkSetWishlistRequest>
{
    public BulkSetWishlistValidator()
    {
        RuleFor(x => x.CardPrintingId).GreaterThan(0);
        RuleFor(x => x.QuantityWanted).GreaterThanOrEqualTo(0);
    }
}
