namespace api.Importing.Swccg;

/// <summary>
/// Client interface for retrieving Star Wars CCG catalog data from swccg-card-json.
/// </summary>
public interface ISwccgApiClient
{
    /// <summary>
    /// Fetches all expansion set definitions from sets.json.
    /// </summary>
    Task<List<SwccgSet>> GetSetsAsync(CancellationToken ct = default);

    /// <summary>
    /// Fetches all Dark Side cards from Dark.json.
    /// </summary>
    Task<List<SwccgCardRecord>> GetDarkCardsAsync(CancellationToken ct = default);

    /// <summary>
    /// Fetches all Light Side cards from Light.json.
    /// </summary>
    Task<List<SwccgCardRecord>> GetLightCardsAsync(CancellationToken ct = default);

    /// <summary>
    /// Fetches all cards across both Dark and Light sides.
    /// </summary>
    Task<List<SwccgCardRecord>> GetAllCardsAsync(CancellationToken ct = default);
}

