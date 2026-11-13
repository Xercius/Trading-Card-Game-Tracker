namespace api.Features._Common;

/// <summary>
/// Request payload for quick-adding copies of a card printing to a user's collection or wishlist.
/// </summary>
public sealed record QuickAddRequest(
    int PrintingId,
    int Quantity);
