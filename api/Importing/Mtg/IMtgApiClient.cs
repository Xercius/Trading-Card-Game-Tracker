namespace api.Importing.Mtg;

/// <summary>
/// Client contract for fetching sets and card search pages from the Scryfall API.
/// </summary>
public interface IMtgApiClient
{
    /// <summary>Fetches all sets from Scryfall (<c>GET /sets</c>).</summary>
    Task<IReadOnlyList<ScryfallSet>> GetSetsAsync(CancellationToken ct = default);

    /// <summary>Fetches all paginated English card printings for a set code (<c>GET /cards/search</c>).</summary>
    Task<IReadOnlyList<ScryfallCard>> GetSetCardsAsync(string setCode, CancellationToken ct = default);
}
