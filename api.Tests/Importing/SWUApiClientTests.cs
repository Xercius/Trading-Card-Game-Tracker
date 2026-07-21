using api.Importing;
using System.Net;
using System.Text;
using System.Web;
using Xunit;

namespace api.Tests.Importing;

/// <summary>
/// Unit tests for <see cref="SWUApiClient"/> covering query-string construction,
/// pagination, rate-limit retry behaviour, and error handling.
/// All tests use stub <see cref="HttpMessageHandler"/> implementations so no real network
/// calls are made.
/// </summary>
public sealed class SWUApiClientTests
{
    private const string BaseAddress = "https://admin.starwarsunlimited.com/api/";

    // ─── helpers ────────────────────────────────────────────────────────────

    private static SWUApiClient CreateClient(HttpMessageHandler handler)
    {
        var http = new HttpClient(handler) { BaseAddress = new Uri(BaseAddress) };
        return new SWUApiClient(http);
    }

    private static string BuildStrapiPage(int page, int pageCount, int id = 1,
        string title = "Test Card", string serialCode = "TC000001", int expansionId = 2)
    {
        var record = new
        {
            id,
            attributes = new
            {
                title,
                subtitle = (string?)null,
                cardUid = id.ToString(),
                serialCode,
                locale = "en",
                cardNumber = id,
                rarity = "Common",
                text = (string?)null,
                artist = (string?)null,
                cost = (int?)null,
                power = (int?)null,
                health = (int?)null,
                arena = (string?)null,
                aspects = (string[]?)null,
                traits = (string[]?)null,
                keywords = (string[]?)null,
                updatedAt = "2025-11-10T16:07:21.000Z",
                type = new { data = new { id = 3, attributes = new { name = "Unit", value = "Unit" } } },
                expansion = new
                {
                    data = new
                    {
                        id = expansionId,
                        attributes = new { name = "Spark of Rebellion", code = "SOR" }
                    }
                },
                variantTypes = new
                {
                    data = new[]
                    {
                        new { id = 46, attributes = new { name = "Standard", variantId = "01", foil = false } }
                    }
                },
                variantOf = new { data = (object?)null },
                reprintOf = new { data = (object?)null },
                artFront = new { data = (object?)null },
                artBack = new { data = (object?)null }
            }
        };

        return System.Text.Json.JsonSerializer.Serialize(new
        {
            data = new[] { record },
            meta = new { pagination = new { page, pageSize = 1, pageCount, total = pageCount } }
        });
    }

    private static string EmptyPage(int page = 1)
        => $"{{\"data\":[],\"meta\":{{\"pagination\":{{\"page\":{page},\"pageSize\":100,\"pageCount\":1,\"total\":0}}}}}}";

    // ─── CapturingHttpMessageHandler ────────────────────────────────────────

    /// <summary>
    /// Records every outgoing request URL and returns a fixed JSON response.
    /// </summary>
    private sealed class CapturingHandler(string responseJson) : HttpMessageHandler
    {
        public List<Uri> CapturedRequests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            CapturedRequests.Add(request.RequestUri!);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseJson, Encoding.UTF8, "application/json")
            });
        }
    }

    /// <summary>
    /// Returns a different page response depending on the <c>pagination[page]</c> query parameter.
    /// </summary>
    private sealed class FakePagedHandler(Dictionary<int, string> pages) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var query = HttpUtility.ParseQueryString(request.RequestUri!.Query);
            int page = int.TryParse(query["pagination[page]"], out int p) ? p : 1;
            string json = pages.TryGetValue(page, out string? r) ? r : EmptyPage(page);

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            });
        }
    }

    /// <summary>
    /// Returns HTTP 429 for the first <paramref name="failCount"/> requests, then succeeds.
    /// </summary>
    private sealed class RateLimitHandler(string successJson, int failCount = 1) : HttpMessageHandler
    {
        private int _callCount;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            if (System.Threading.Interlocked.Increment(ref _callCount) <= failCount)
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.TooManyRequests));

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(successJson, Encoding.UTF8, "application/json")
            });
        }
    }

    /// <summary>Returns a fixed HTTP error status for every request.</summary>
    private sealed class ErrorHandler(HttpStatusCode statusCode) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
            => Task.FromResult(new HttpResponseMessage(statusCode));
    }

    // ─── GetAllCardsAsync – query-string building ────────────────────────────

    [Fact]
    public async Task GetAllCardsAsync_WithExpansionCode_IncludesCodeFilter()
    {
        // When only ExpansionCode is supplied the query must use the code-based filter.
        var handler = new CapturingHandler(BuildStrapiPage(1, 1));
        var client = CreateClient(handler);

        await client.GetAllCardsAsync(new SWUCardFilter(ExpansionCode: "SOR"));

        var query = Uri.UnescapeDataString(handler.CapturedRequests[0].Query);
        Assert.Contains("filters[expansion][code][$eq]=SOR", query);
        Assert.DoesNotContain("filters[expansion][id]", query);
    }

    [Fact]
    public async Task GetAllCardsAsync_WithExpansionId_IncludesIdFilter()
    {
        // When ExpansionId is supplied the query must use the numeric ID filter.
        var handler = new CapturingHandler(BuildStrapiPage(1, 1));
        var client = CreateClient(handler);

        await client.GetAllCardsAsync(new SWUCardFilter(ExpansionId: 5));

        var query = Uri.UnescapeDataString(handler.CapturedRequests[0].Query);
        Assert.Contains("filters[expansion][id][$eq]=5", query);
        Assert.DoesNotContain("filters[expansion][code]", query);
    }

    [Fact]
    public async Task GetAllCardsAsync_WithUpdatedSince_IncludesDateFilter()
    {
        // When UpdatedSince is set the query must contain the ISO-8601 UTC timestamp.
        var handler = new CapturingHandler(BuildStrapiPage(1, 1));
        var client = CreateClient(handler);

        var since = new DateTimeOffset(2025, 11, 1, 0, 0, 0, TimeSpan.Zero);
        await client.GetAllCardsAsync(new SWUCardFilter(ExpansionCode: "SOR", UpdatedSince: since));

        var query = Uri.UnescapeDataString(handler.CapturedRequests[0].Query);
        Assert.Contains("filters[updatedAt][$gt]", query);
        Assert.Contains("2025-11-01T00:00:00.000Z", query);
    }

    [Fact]
    public async Task GetAllCardsAsync_WithoutUpdatedSince_OmitsDateFilter()
    {
        // When UpdatedSince is null the query must NOT contain a date filter.
        var handler = new CapturingHandler(BuildStrapiPage(1, 1));
        var client = CreateClient(handler);

        await client.GetAllCardsAsync(new SWUCardFilter(ExpansionCode: "SOR"));

        var query = Uri.UnescapeDataString(handler.CapturedRequests[0].Query);
        Assert.DoesNotContain("filters[updatedAt]", query);
    }

    [Fact]
    public async Task GetAllCardsAsync_AlwaysSortsByUpdatedAtAsc()
    {
        // The sort order must always be updatedAt:asc to support stable incremental imports.
        var handler = new CapturingHandler(BuildStrapiPage(1, 1));
        var client = CreateClient(handler);

        await client.GetAllCardsAsync(new SWUCardFilter(ExpansionCode: "SOR"));

        var query = Uri.UnescapeDataString(handler.CapturedRequests[0].Query);
        Assert.Contains("updatedAt:asc", query);
        Assert.Contains("cardNumber:asc", query);
    }

    [Fact]
    public async Task GetAllCardsAsync_AlwaysRequestsEnLocale()
    {
        // Every request must include locale=en.
        var handler = new CapturingHandler(BuildStrapiPage(1, 1));
        var client = CreateClient(handler);

        await client.GetAllCardsAsync(new SWUCardFilter(ExpansionCode: "SOR"));

        var query = Uri.UnescapeDataString(handler.CapturedRequests[0].Query);
        Assert.Contains("locale=en", query);
    }

    // ─── GetAllCardsAsync – pagination ──────────────────────────────────────

    [Fact]
    public async Task GetAllCardsAsync_FetchesAllPages_ReturnsFlatList()
    {
        // A three-page response must yield all three cards in a single flat list.
        var pages = new Dictionary<int, string>
        {
            [1] = BuildStrapiPage(1, 3, id: 1, title: "Card A", serialCode: "TC000001"),
            [2] = BuildStrapiPage(2, 3, id: 2, title: "Card B", serialCode: "TC000002"),
            [3] = BuildStrapiPage(3, 3, id: 3, title: "Card C", serialCode: "TC000003"),
        };
        var client = CreateClient(new FakePagedHandler(pages));

        var result = await client.GetAllCardsAsync(new SWUCardFilter(ExpansionCode: "SOR"));

        Assert.Equal(3, result.Count);
        Assert.Contains(result, r => r.Attributes?.Title == "Card A");
        Assert.Contains(result, r => r.Attributes?.Title == "Card B");
        Assert.Contains(result, r => r.Attributes?.Title == "Card C");
    }

    [Fact]
    public async Task GetAllCardsAsync_SinglePage_ReturnsCards()
    {
        // A single-page response must return all cards without extra requests.
        var json = BuildStrapiPage(1, 1, id: 10, title: "Solo Card", serialCode: "SC000010");
        var handler = new CapturingHandler(json);
        var client = CreateClient(handler);

        var result = await client.GetAllCardsAsync(new SWUCardFilter(ExpansionCode: "SOR"));

        Assert.Single(result);
        Assert.Equal("Solo Card", result[0].Attributes?.Title);
        Assert.Single(handler.CapturedRequests);
    }

    [Fact]
    public async Task GetAllCardsAsync_EmptyResponse_ReturnsEmptyList()
    {
        // An empty data array must return an empty list without throwing.
        var client = CreateClient(new CapturingHandler(EmptyPage()));

        var result = await client.GetAllCardsAsync(new SWUCardFilter(ExpansionCode: "SOR"));

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetAllCardsAsync_SendsCorrectPageNumbers()
    {
        // Page numbers in the request query string must increment from 1 through pageCount.
        var pages = new Dictionary<int, string>
        {
            [1] = BuildStrapiPage(1, 2, id: 1, title: "Card A", serialCode: "PA000001"),
            [2] = BuildStrapiPage(2, 2, id: 2, title: "Card B", serialCode: "PB000001"),
        };
        var capturing = new CapturingFakePagedHandler(pages);
        var client = CreateClient(capturing);

        await client.GetAllCardsAsync(new SWUCardFilter(ExpansionCode: "SOR"));

        Assert.Equal(2, capturing.CapturedRequests.Count);
        var q1 = Uri.UnescapeDataString(capturing.CapturedRequests[0].Query);
        var q2 = Uri.UnescapeDataString(capturing.CapturedRequests[1].Query);
        Assert.Contains("pagination[page]=1", q1);
        Assert.Contains("pagination[page]=2", q2);
    }

    // ─── Rate-limit retry ────────────────────────────────────────────────────

    [Fact]
    public async Task GetAllCardsAsync_RetriesOnce_On429_ThenSucceeds()
    {
        // A single 429 response must trigger a retry and eventually return cards.
        var json = BuildStrapiPage(1, 1, id: 99, title: "Retry Card", serialCode: "RC000099");
        var client = CreateClient(new RateLimitHandler(json, failCount: 1));

        var result = await client.GetAllCardsAsync(new SWUCardFilter(ExpansionCode: "SOR"));

        Assert.Single(result);
        Assert.Equal("Retry Card", result[0].Attributes?.Title);
    }

    [Fact]
    public async Task GetAllCardsAsync_ExceedsMaxRetries_ThrowsHttpRequestException()
    {
        // When 429 persists beyond MaxRetries (3), the client must give up and throw.
        var client = CreateClient(new RateLimitHandler(EmptyPage(), failCount: 10));

        await Assert.ThrowsAsync<HttpRequestException>(
            () => client.GetAllCardsAsync(new SWUCardFilter(ExpansionCode: "SOR")));
    }

    // ─── Error handling ──────────────────────────────────────────────────────

    [Fact]
    public async Task GetAllCardsAsync_Throws_HttpRequestException_On500()
    {
        // A 500 Internal Server Error must surface as HttpRequestException immediately (no retry).
        var client = CreateClient(new ErrorHandler(HttpStatusCode.InternalServerError));

        await Assert.ThrowsAsync<HttpRequestException>(
            () => client.GetAllCardsAsync(new SWUCardFilter(ExpansionCode: "SOR")));
    }

    [Fact]
    public async Task GetAllCardsAsync_Throws_HttpRequestException_On404()
    {
        // A 404 Not Found must surface as HttpRequestException (no retry).
        var client = CreateClient(new ErrorHandler(HttpStatusCode.NotFound));

        await Assert.ThrowsAsync<HttpRequestException>(
            () => client.GetAllCardsAsync(new SWUCardFilter(ExpansionCode: "SOR")));
    }

    [Fact]
    public async Task GetAllCardsAsync_CancellationToken_PropagatesOperationCancelledException()
    {
        // When the CancellationToken is already cancelled, the call must throw OperationCanceledException.
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var client = CreateClient(new CapturingHandler(EmptyPage()));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => client.GetAllCardsAsync(new SWUCardFilter(ExpansionCode: "SOR"), cts.Token));
    }

    // ─── TryResolveExpansionIdAsync ──────────────────────────────────────────

    [Fact]
    public async Task TryResolveExpansionIdAsync_ReturnsId_WhenFound()
    {
        // Must return the expansion ID from the first card's expansion relationship.
        var json = BuildStrapiPage(1, 1, id: 1, expansionId: 42);
        var handler = new CapturingHandler(json);
        var client = CreateClient(handler);

        int? id = await client.TryResolveExpansionIdAsync("SOR");

        Assert.Equal(42, id);
    }

    [Fact]
    public async Task TryResolveExpansionIdAsync_ReturnsNull_WhenEmptyResponse()
    {
        // An empty data array must cause the method to return null without throwing.
        var client = CreateClient(new CapturingHandler(EmptyPage()));

        int? id = await client.TryResolveExpansionIdAsync("SOR");

        Assert.Null(id);
    }

    [Fact]
    public async Task TryResolveExpansionIdAsync_ReturnsNull_OnHttpError()
    {
        // Any HTTP error must be swallowed and null returned (callers fall back to code filter).
        var client = CreateClient(new ErrorHandler(HttpStatusCode.InternalServerError));

        int? id = await client.TryResolveExpansionIdAsync("SOR");

        Assert.Null(id);
    }

    [Fact]
    public async Task TryResolveExpansionIdAsync_SendsCodeFilter_And_PageSize1()
    {
        // The discovery request must use the code filter with pageSize=1 (minimal data transfer).
        var handler = new CapturingHandler(EmptyPage());
        var client = CreateClient(handler);

        await client.TryResolveExpansionIdAsync("SOTG");

        var query = Uri.UnescapeDataString(handler.CapturedRequests[0].Query);
        Assert.Contains("filters[expansion][code][$eq]=SOTG", query);
        Assert.Contains("pagination[pageSize]=1", query);
    }

    // ─── Additional handler ──────────────────────────────────────────────────

    /// <summary>
    /// A paged handler that also captures all request URIs, used when both capturing
    /// and paged-response behaviour are needed simultaneously.
    /// </summary>
    private sealed class CapturingFakePagedHandler(Dictionary<int, string> pages) : HttpMessageHandler
    {
        public List<Uri> CapturedRequests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            CapturedRequests.Add(request.RequestUri!);
            var query = HttpUtility.ParseQueryString(request.RequestUri!.Query);
            int page = int.TryParse(query["pagination[page]"], out int p) ? p : 1;
            string json = pages.TryGetValue(page, out string? r) ? r : EmptyPage(page);

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            });
        }
    }
}
