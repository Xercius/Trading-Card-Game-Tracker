namespace api.Features.ImportExport.Dtos;

public sealed record ExportDeckCard(
    int CardPrintingId,
    int InDeck,
    int Idea,
    int Acquire,
    int Proxy,
    int Sideboard = 0,
    bool IsStarting = false,
    bool IsCommander = false,
    string? CardName = null,
    string? SetCode = null,
    string? SetName = null,
    string? Number = null,
    string? Style = null);

public sealed record ExportDeck(
    string Game,
    string Name,
    string? Description,
    List<ExportDeckCard> Cards,
    string? Format = null,
    string? Side = null);

public sealed record ExportCollectionItem(
    int CardPrintingId,
    int QtyOwned,
    int QtyProxyOwned,
    string? Game = null,
    string? CardName = null,
    string? SetCode = null,
    string? SetName = null,
    string? Number = null,
    string? Style = null);

public sealed record ExportWishlistSourceItem(
    string? DeckGame,
    string? DeckName,
    int Qty);

public sealed record ExportWishlistItem(
    int CardPrintingId,
    int Qty,
    string? Game = null,
    string? CardName = null,
    string? SetCode = null,
    string? SetName = null,
    string? Number = null,
    string? Style = null,
    List<ExportWishlistSourceItem>? Sources = null);

public sealed record ExportPayload(
    int Version,
    List<ExportCollectionItem> Collection,
    List<ExportWishlistItem> Wishlist,
    List<ExportDeck> Decks);

public sealed record ImportPayload(
    int Version = 1,
    List<ExportCollectionItem>? Collection = null,
    List<ExportWishlistItem>? Wishlist = null,
    List<ExportDeck>? Decks = null);

public sealed record ImportUnresolvedItem(
    string Section,
    int CardPrintingId,
    string? Game,
    string? CardName,
    string? SetCode,
    string? Number,
    string Reason);

public sealed record ImportSummaryCounts(
    int Collection,
    int Wishlist,
    int Decks,
    int DeckCards,
    int ResolvedByNaturalKey = 0,
    int UnresolvedCount = 0);

public sealed record ImportJsonResponse(
    ImportSummaryCounts Imported,
    IReadOnlyList<ImportUnresolvedItem>? Unresolved = null);
