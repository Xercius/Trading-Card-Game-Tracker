using api.Features.Users.Dtos;
using FluentValidation;

namespace api.Features.Users.Validation;

public sealed class CreateUserValidator : AbstractValidator<CreateUserRequest>
{
    public CreateUserValidator()
    {
        RuleFor(x => x.Username).NotEmpty().MaximumLength(120);
        RuleFor(x => x.DisplayName).NotEmpty().MaximumLength(120);
    }
}
