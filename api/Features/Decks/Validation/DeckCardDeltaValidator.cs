using api.Features.Decks.Dtos;
using FluentValidation;

namespace api.Features.Decks.Validation;

public sealed class DeckCardDeltaValidator : AbstractValidator<DeckCardDeltaRequest>
{
    public DeckCardDeltaValidator()
    {
        RuleFor(x => x.CardPrintingId).GreaterThan(0);
    }
}
