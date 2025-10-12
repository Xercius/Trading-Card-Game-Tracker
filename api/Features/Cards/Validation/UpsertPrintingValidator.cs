using api.Features.Cards.Dtos;
using FluentValidation;

namespace api.Features.Cards.Validation;

public sealed class UpsertPrintingValidator : AbstractValidator<UpsertPrintingRequest>
{
    public UpsertPrintingValidator()
    {
        RuleFor(x => x.CardId).GreaterThan(0);
        RuleFor(x => x.Set).MaximumLength(64).When(x => x.Set is not null);
        RuleFor(x => x.Number).MaximumLength(32).When(x => x.Number is not null);
        RuleFor(x => x.Rarity).MaximumLength(32).When(x => x.Rarity is not null);
        RuleFor(x => x.Style).MaximumLength(64).When(x => x.Style is not null);
    }
}
