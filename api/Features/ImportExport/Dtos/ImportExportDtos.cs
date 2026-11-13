namespace api.Features.ImportExport.Dtos;

public sealed record ExportDeckCard(int CardPrintingId, int InDeck, int Idea, int Acquire, int Proxy);
public sealed record ExportDeck(string Game, string Name, string? Description, List<ExportDeckCard> Cards);
public sealed record ExportCollectionItem(int CardPrintingId, int QtyOwned, int QtyProxyOwned);
public sealed record ExportWishlistItem(int CardPrintingId, int Qty);

public sealed record ExportPayload(
    int Version,
    object? User,
    List<ExportCollectionItem> Collection,
    List<ExportWishlistItem> Wishlist,
    List<ExportDeck> Decks);

public sealed record ImportPayload(
    int Version = 1,
    List<ExportCollectionItem>? Collection = null,
    List<ExportWishlistItem>? Wishlist = null,
    List<ExportDeck>? Decks = null);

public sealed record ImportSummaryCounts(int Collection, int Wishlist, int Decks, int DeckCards);
public sealed record ImportJsonResponse(ImportSummaryCounts Imported);
