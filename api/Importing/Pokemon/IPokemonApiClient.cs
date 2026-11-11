namespace api.Importing.Pokemon;

/// <summary>
/// Client contract for fetching sets, series, and card payloads from TCGdex.
/// </summary>
public interface IPokemonApiClient
{
    Task<IReadOnlyList<TcgdexSetResume>> GetSetsAsync(CancellationToken ct = default);
    Task<TcgdexSetDetail?> GetSetAsync(string setId, CancellationToken ct = default);
    Task<TcgdexCard?> GetCardAsync(string cardId, CancellationToken ct = default);
    Task<IReadOnlyList<TcgdexSerieResume>> GetSeriesAsync(CancellationToken ct = default);
}
