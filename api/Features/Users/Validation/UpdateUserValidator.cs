using FluentValidation;
using api.Features.Users.Dtos;

namespace api.Features.Users.Validation;

public sealed class UpdateUserValidator : AbstractValidator<UpdateUserRequest>
{
    public UpdateUserValidator()
    {
        RuleFor(x => x.Username).NotEmpty().MaximumLength(120);
        RuleFor(x => x.DisplayName).NotEmpty().MaximumLength(120);
    }
}
