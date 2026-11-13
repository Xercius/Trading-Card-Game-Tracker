using api.Features.ImportExport.Dtos;
using FluentValidation;

namespace api.Features.ImportExport.Validation;

/// <summary>Validates <see cref="ImportPayload"/> requests.</summary>
public sealed class ImportPayloadValidator : AbstractValidator<ImportPayload>
{
    public ImportPayloadValidator()
    {
        RuleFor(x => x.Version).Equal(1).WithMessage("Unsupported version.");
    }
}
