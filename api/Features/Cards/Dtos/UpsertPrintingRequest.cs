namespace api.Features.Cards.Dtos;

/// <summary>
/// Request DTO for creating a new card printing or updating an existing one.
/// Supports both insert (Id is null) and update (Id is provided) operations.
/// </summary>
/// <remarks>
/// This DTO follows the "Upsert" pattern where a single endpoint/DTO handles both
/// create and update operations based on whether an Id is provided:
/// 
/// - Create: When Id is null, creates a new printing for the specified CardId
/// - Update: When Id is provided, updates the existing printing with that Id
/// 
/// The ImageUrlSet flag is a special boolean used to distinguish between:
/// - "I want to set ImageUrl to null" (ImageUrlSet=true, ImageUrl=null)
/// - "I'm not changing ImageUrl at all" (ImageUrlSet=false)
/// 
/// This is necessary because null values in C# can be ambiguous in update scenarios.
/// Without this flag, we couldn't differentiate between "clear the image" vs "don't touch the image".
/// 
/// All string properties except ImageUrl are nullable to support partial updates,
/// though typically all fields should be provided for consistency.
/// 
/// Used by:
/// - POST /api/cards/{cardId}/printings - Create new printing
/// - PUT /api/cards/{cardId}/printings/{id} - Update existing printing
/// </remarks>
/// <param name="Id">Unique identifier of the printing to update. Null when creating a new printing.</param>
/// <param name="CardId">Foreign key to the parent Card this printing belongs to. Required for both create and update.</param>
/// <param name="Set">Name of the card set/expansion (e.g., "The First Chapter", "Ursula's Return"). Null to keep existing value in updates.</param>
/// <param name="Number">Collector or card number within the set (e.g., "001", "147/204", "TFC-001"). Null to keep existing value in updates.</param>
/// <param name="Rarity">Rarity classification (e.g., "Common", "Rare", "Super Rare", "Legendary"). Null to keep existing value in updates.</param>
/// <param name="Style">Print style or finish (e.g., "Standard", "Foil", "Hyperspace", "Showcase"). Null to keep existing value in updates.</param>
/// <param name="ImageUrl">URL to the card's image. Null to clear the image (when ImageUrlSet=true) or keep existing (when ImageUrlSet=false).</param>
/// <param name="ImageUrlSet">Flag indicating whether ImageUrl should be updated. True means apply the ImageUrl value (even if null), false means don't touch it. Defaults to false.</param>
public sealed record UpsertPrintingRequest(
    int? Id,
    int CardId,
    string? Set,
    string? Number,
    string? Rarity,
    string? Style,
    string? ImageUrl,
    bool ImageUrlSet = false);
