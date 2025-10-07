using FluentValidation;
using api.Features.Decks.Dtos;

namespace api.Features.Decks.Validation;

public sealed class UpsertDeckCardValidator : AbstractValidator<UpsertDeckCardFullRequest>
{
    public UpsertDeckCardValidator()
    {
        RuleFor(x => x.CardPrintingId).GreaterThan(0);
        RuleFor(x => x.QuantityInDeck).GreaterThanOrEqualTo(0);
        RuleFor(x => x.QuantityIdea).GreaterThanOrEqualTo(0);
        RuleFor(x => x.QuantityAcquire).GreaterThanOrEqualTo(0);
        RuleFor(x => x.QuantityProxy).GreaterThanOrEqualTo(0);
    }
}
