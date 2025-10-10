namespace api.Shared;

public sealed record Paged<T>(IReadOnlyList<T> Items, int Total, int Page, int PageSize);
