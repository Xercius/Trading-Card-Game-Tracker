namespace api.Features.Decks.Dtos;

/// <summary>Request payload to add missing cards from a deck to the wishlist.</summary>
public sealed record AddMissingToWishlistRequest(
    int? CardPrintingId = null,
    bool IncludeProxies = false);

/// <summary>Summary returned after adding missing deck cards to the wishlist.</summary>
public sealed record AddMissingToWishlistResponse(
    int CardsUpdated,
    int TotalCopiesNeeded);
