using api.Features.Cards.Dtos;
using FluentValidation;

namespace api.Features.Cards.Validation;

/// <summary>
/// FluentValidation validator for UpsertPrintingRequest that ensures data integrity
/// when creating or updating card printing records.
/// 
/// Validates that:
/// - CardId references a valid card (must be positive)
/// - String fields respect database column length constraints to prevent truncation errors
/// 
/// This validator is automatically discovered and integrated with ASP.NET Core's validation
/// pipeline, running before the request reaches the controller action.
/// </summary>
public sealed class UpsertPrintingValidator : AbstractValidator<UpsertPrintingRequest>
{
    /// <summary>
    /// Configures validation rules for card printing upsert operations.
    /// 
    /// Validation rules applied:
    /// - CardId: Must be greater than 0; this validator does not check existence.
    /// - Set: Maximum 64 characters (when provided) - matches database column constraint
    /// - Number: Maximum 32 characters (when provided) - matches database column constraint
    /// - Rarity: Maximum 32 characters (when provided) - matches database column constraint
    /// - Style: Maximum 64 characters (when provided) - matches database column constraint
    /// 
    /// All string fields are optional (nullable) and only validated when non-null.
    /// 
    /// </summary>
    public UpsertPrintingValidator()
    {
        // Ensure CardId references a valid card record (primary keys start at 1)
        RuleFor(x => x.CardId).GreaterThan(0);
        
        // Validate optional Set field length matches CardPrinting.Set column (max 64 chars)
        RuleFor(x => x.Set).MaximumLength(64).When(x => x.Set is not null);
        
        // Validate optional Number field length matches CardPrinting.Number column (max 32 chars)
        RuleFor(x => x.Number).MaximumLength(32).When(x => x.Number is not null);
        
        // Validate optional Rarity field length matches CardPrinting.Rarity column (max 32 chars)
        RuleFor(x => x.Rarity).MaximumLength(32).When(x => x.Rarity is not null);
        
        // Validate optional Style field length matches CardPrinting.Style column (max 64 chars)
        RuleFor(x => x.Style).MaximumLength(64).When(x => x.Style is not null);
    }
}
