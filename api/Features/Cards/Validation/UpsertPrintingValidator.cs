using FluentValidation;
using api.Features.Cards.Dtos;

namespace api.Features.Cards.Validation;

public sealed class UpsertPrintingValidator : AbstractValidator<UpsertPrintingRequest>
{
    public UpsertPrintingValidator()
    {
        RuleFor(x => x.CardId).GreaterThan(0);
        RuleFor(x => x.Set).MaximumLength(100).When(x => x.Set is not null);
        RuleFor(x => x.Number).MaximumLength(100).When(x => x.Number is not null);
        RuleFor(x => x.Rarity).MaximumLength(100).When(x => x.Rarity is not null);
        RuleFor(x => x.Style).MaximumLength(100).When(x => x.Style is not null);
    }
}
