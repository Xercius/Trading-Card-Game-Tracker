using api.Features.Admin.Sync.Dtos;
using FluentValidation;

namespace api.Features.Admin.Sync.Validation;

/// <summary>Validates <see cref="AdminMtgCardsSyncRequest"/>.</summary>
public sealed class AdminMtgCardsSyncRequestValidator : AbstractValidator<AdminMtgCardsSyncRequest>
{
    public AdminMtgCardsSyncRequestValidator()
    {
        RuleFor(x => x.SetCodes)
            .NotEmpty()
            .WithMessage("At least one set code is required.");

        RuleForEach(x => x.SetCodes)
            .NotEmpty()
            .MaximumLength(32);
    }
}
