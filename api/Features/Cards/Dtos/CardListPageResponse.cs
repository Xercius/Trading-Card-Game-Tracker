namespace api.Features.Cards.Dtos;

/// <summary>
/// Response DTO for paginated card list results with virtualized scrolling support.
/// Wraps a collection of card items along with pagination metadata for efficient large dataset handling.
/// </summary>
/// <remarks>
/// This DTO supports virtualized/infinite scroll pagination patterns where the client
/// needs to know the next offset (skip value) to fetch subsequent pages.
/// 
/// - Items: The actual card data for the current page
/// - Total: Overall count of cards matching the query (null if not computed for performance)
/// - NextSkip: The skip/offset value to use for fetching the next page (null if no more results)
/// 
/// The virtualized pagination approach minimizes server load by not always computing total counts
/// and provides smooth scrolling UX by telling the client exactly what offset to request next.
/// 
/// Typically returned by:
/// - GET /api/cards - Card listing with virtualized pagination
/// - Card search endpoints with large result sets
/// </remarks>
public sealed class CardListPageResponse
{
    /// <summary>
    /// Collection of card items for the current page/view.
    /// Each item contains card summary and primary printing information.
    /// </summary>
    public required IReadOnlyList<CardListItemResponse> Items { get; set; }

    /// <summary>
    /// Total count of cards matching the query across all pages.
    /// May be null if total count computation was skipped for performance.
    /// When null, clients should rely on NextSkip to determine if more results exist.
    /// </summary>
    public int? Total { get; set; }

    /// <summary>
    /// The skip/offset value to use when requesting the next page of results.
    /// Null if this is the last page (no more results available).
    /// Clients should pass this value as the 'skip' query parameter on the next request.
    /// </summary>
    public int? NextSkip { get; set; }
}
