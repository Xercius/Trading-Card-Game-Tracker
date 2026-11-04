using api.Features.Collections.Dtos;
using FluentValidation;

namespace api.Features.Collections.Validation;

public sealed class SetUserCardQuantitiesValidator : AbstractValidator<SetUserCardQuantitiesRequest>
{
    public SetUserCardQuantitiesValidator()
    {
        When(x => x.QuantityOwned.HasValue, () =>
            RuleFor(x => x.QuantityOwned!.Value).GreaterThanOrEqualTo(0));
        When(x => x.QuantityWanted.HasValue, () =>
            RuleFor(x => x.QuantityWanted!.Value).GreaterThanOrEqualTo(0));
        When(x => x.QuantityProxyOwned.HasValue, () =>
            RuleFor(x => x.QuantityProxyOwned!.Value).GreaterThanOrEqualTo(0));
    }
}
