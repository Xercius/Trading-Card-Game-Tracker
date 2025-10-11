using api.Features.Decks.Dtos;
using FluentValidation;

namespace api.Features.Decks.Validation;

public sealed class UpdateDeckValidator : AbstractValidator<UpdateDeckRequest>
{
    public UpdateDeckValidator()
    {
        RuleFor(x => x.Game).NotEmpty().MaximumLength(80);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(120);
    }
}
