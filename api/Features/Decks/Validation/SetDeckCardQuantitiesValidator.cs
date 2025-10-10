using api.Features.Decks.Dtos;
using FluentValidation;

namespace api.Features.Decks.Validation;

public sealed class SetDeckCardQuantitiesValidator : AbstractValidator<SetDeckCardQuantitiesRequest>
{
    public SetDeckCardQuantitiesValidator()
    {
        RuleFor(x => x.QuantityInDeck).GreaterThanOrEqualTo(0);
        RuleFor(x => x.QuantityIdea).GreaterThanOrEqualTo(0);
        RuleFor(x => x.QuantityAcquire).GreaterThanOrEqualTo(0);
        RuleFor(x => x.QuantityProxy).GreaterThanOrEqualTo(0);
    }
}
