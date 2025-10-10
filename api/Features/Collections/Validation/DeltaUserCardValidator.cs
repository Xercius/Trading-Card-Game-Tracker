using api.Features.Collections.Dtos;
using FluentValidation;

namespace api.Features.Collections.Validation;

public sealed class DeltaUserCardValidator : AbstractValidator<DeltaUserCardRequest>
{
    public DeltaUserCardValidator()
    {
        RuleFor(x => x.CardPrintingId).GreaterThan(0);
    }
}
