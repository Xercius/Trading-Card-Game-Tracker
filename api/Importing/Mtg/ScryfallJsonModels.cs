namespace api.Importing.Mtg;

/// <summary>
/// Generic Scryfall paginated list envelope returned by <c>/sets</c> and <c>/cards/search</c>.
/// </summary>
public sealed record ScryfallListResponse<T>(
    string Object,
    bool HasMore,
    string? NextPage,
    int? TotalCards,
    IReadOnlyList<T> Data
);

/// <summary>
/// Scryfall set metadata object returned by <c>GET /sets</c>.
/// </summary>
public sealed record ScryfallSet(
    string Id,
    string Code,
    string Name,
    string SetType,
    string? Block,
    string? BlockCode,
    string? ReleasedAt,
    int CardCount,
    bool Digital,
    bool FoilOnly,
    bool NonfoilOnly,
    string? IconSvgUri,
    string? SearchUri
);

/// <summary>
/// Scryfall card object returned by <c>GET /cards/search</c>.
/// </summary>
public sealed record ScryfallCard(
    string Id,
    string? OracleId,
    string Lang,
    string Name,
    string? PrintedName,
    string? Layout,
    string? ManaCost,
    decimal? Cmc,
    string? TypeLine,
    string? PrintedTypeLine,
    string? OracleText,
    string? PrintedText,
    string? FlavorText,
    string? Power,
    string? Toughness,
    string? Loyalty,
    string? Defense,
    IReadOnlyList<string>? Colors,
    IReadOnlyList<string>? ColorIdentity,
    IReadOnlyList<string>? Keywords,
    string? Rarity,
    string? CollectorNumber,
    string? Set,
    string? SetName,
    IReadOnlyList<string>? Finishes,
    string? Frame,
    IReadOnlyList<string>? FrameEffects,
    string? BorderColor,
    bool FullArt,
    bool Textless,
    bool Promo,
    IReadOnlyList<string>? PromoTypes,
    bool Variation,
    string? Artist,
    string? ReleasedAt,
    ScryfallImageUris? ImageUris,
    IReadOnlyList<ScryfallCardFace>? CardFaces
);

/// <summary>
/// Individual face on a multi-faced Scryfall card (transform, modal_dfc, split, flip, adventure, etc.).
/// </summary>
public sealed record ScryfallCardFace(
    string? Name,
    string? PrintedName,
    string? OracleId,
    string? ManaCost,
    string? TypeLine,
    string? PrintedTypeLine,
    string? OracleText,
    string? PrintedText,
    string? FlavorText,
    string? Power,
    string? Toughness,
    string? Loyalty,
    string? Defense,
    IReadOnlyList<string>? Colors,
    string? Artist,
    ScryfallImageUris? ImageUris
);

/// <summary>
/// Image URI variants provided by Scryfall on cards or card faces.
/// </summary>
public sealed record ScryfallImageUris(
    string? Small,
    string? Normal,
    string? Large,
    string? Png,
    string? ArtCrop,
    string? BorderCrop
);
