using FluentValidation;
using api.Features.Wishlists.Dtos;

namespace api.Features.Wishlists.Validation;

public sealed class MoveToCollectionValidator : AbstractValidator<MoveToCollectionRequest>
{
    public MoveToCollectionValidator()
    {
        RuleFor(x => x.CardPrintingId).GreaterThan(0);
        RuleFor(x => x.Quantity).GreaterThan(0);
    }
}
