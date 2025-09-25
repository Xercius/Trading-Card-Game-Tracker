using FluentValidation;
using api.Features.Collections.Dtos;

namespace api.Features.Collections.Validation;

public sealed class DeltaUserCardValidator : AbstractValidator<DeltaUserCardRequest>
{
    public DeltaUserCardValidator()
    {
        RuleFor(x => x.CardPrintingId).GreaterThan(0);
    }
}
