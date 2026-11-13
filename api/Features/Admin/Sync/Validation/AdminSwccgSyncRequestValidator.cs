using api.Features.Admin.Sync.Dtos;
using FluentValidation;

namespace api.Features.Admin.Sync.Validation;

/// <summary>Validates <see cref="AdminSwccgSyncRequest"/>.</summary>
public sealed class AdminSwccgSyncRequestValidator : AbstractValidator<AdminSwccgSyncRequest>
{
    public AdminSwccgSyncRequestValidator()
    {
        RuleFor(x => x.Limit).GreaterThanOrEqualTo(1).When(x => x.Limit.HasValue);
    }
}
