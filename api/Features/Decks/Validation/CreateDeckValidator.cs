using api.Features.Decks.Dtos;
using FluentValidation;

namespace api.Features.Decks.Validation;

public sealed class CreateDeckValidator : AbstractValidator<CreateDeckRequest>
{
    public CreateDeckValidator()
    {
        RuleFor(x => x.Game).NotEmpty().MaximumLength(80);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(120);
    }
}
