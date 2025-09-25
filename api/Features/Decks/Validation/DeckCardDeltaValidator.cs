using FluentValidation;
using api.Features.Decks.Dtos;

namespace api.Features.Decks.Validation;

public sealed class DeckCardDeltaValidator : AbstractValidator<DeckCardDeltaRequest>
{
    public DeckCardDeltaValidator()
    {
        RuleFor(x => x.CardPrintingId).GreaterThan(0);
    }
}
