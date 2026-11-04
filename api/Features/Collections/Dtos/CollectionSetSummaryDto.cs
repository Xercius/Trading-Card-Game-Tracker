namespace api.Features.Collections.Dtos;

/// <summary>
/// Overall response summarizing collection progress across all sets for a given game.
/// </summary>
public sealed record CollectionSetSummaryResponse(
    IReadOnlyList<CollectionSetSummaryDto> Sets,
    int TotalUniqueCardsOwned,
    int TotalUniqueCardsInGame,
    double OverallCompletionPercentage
);

/// <summary>
/// Summary of collection completion for a specific card set.
/// </summary>
public sealed record CollectionSetSummaryDto(
    int SetId,
    string SetCode,
    string SetName,
    int? ReleaseYear,
    DateOnly? ReleasedAt,
    bool IsVirtualSet,
    int TotalCards,
    int OwnedCards,
    double CompletionPercentage,
    IReadOnlyList<CollectionSubsetSummaryDto> Subsets
);

/// <summary>
/// Summary of collection completion for a specific treatment subset within a set.
/// </summary>
public sealed record CollectionSubsetSummaryDto(
    int? TreatmentId,
    string TreatmentName,
    int TotalCards,
    int OwnedCards,
    double CompletionPercentage
);

