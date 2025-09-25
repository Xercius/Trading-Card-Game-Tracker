using FluentValidation;
using api.Features.Collections.Dtos;

namespace api.Features.Collections.Validation;

public sealed class SetUserCardQuantitiesValidator : AbstractValidator<SetUserCardQuantitiesRequest>
{
    public SetUserCardQuantitiesValidator()
    {
        RuleFor(x => x.QuantityOwned).GreaterThanOrEqualTo(0);
        RuleFor(x => x.QuantityWanted).GreaterThanOrEqualTo(0);
        RuleFor(x => x.QuantityProxyOwned).GreaterThanOrEqualTo(0);
    }
}
