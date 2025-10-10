using api.Features.Collections.Dtos;
using FluentValidation;

namespace api.Features.Collections.Validation;

public sealed class UpsertUserCardValidator : AbstractValidator<UpsertUserCardRequest>
{
    public UpsertUserCardValidator()
    {
        RuleFor(x => x.CardPrintingId).GreaterThan(0);
        RuleFor(x => x.QuantityOwned).GreaterThanOrEqualTo(0);
        RuleFor(x => x.QuantityWanted).GreaterThanOrEqualTo(0);
        RuleFor(x => x.QuantityProxyOwned).GreaterThanOrEqualTo(0);
    }
}
