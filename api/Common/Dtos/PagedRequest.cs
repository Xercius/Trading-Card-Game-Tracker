namespace api.Common.Dtos;

public sealed record PagedRequest(int Page = 1, int PageSize = 50, string? Sort = null, string? Query = null);
