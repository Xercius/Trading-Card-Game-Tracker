using FluentValidation;
using api.Features.Values.Dtos;

namespace api.Features.Values.Validation;

public sealed class RefreshItemValidator : AbstractValidator<RefreshItemRequest>
{
    public RefreshItemValidator()
    {
        RuleFor(x => x.CardPrintingId).GreaterThan(0);
        RuleFor(x => x.PriceCents).GreaterThanOrEqualTo(0);
    }
}
