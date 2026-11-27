using api.Tests.Fixtures;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace api.Tests;

public class SingleServerHostingTests(CustomWebApplicationFactory factory) : IClassFixture<CustomWebApplicationFactory>
{
    [Fact]
    public async Task UnmatchedApiRoute_ReturnsNotFoundProblemDetails_NotSpaFallback()
    {
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/does-not-exist/subpath");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal(404, problem!.Status);
    }

    [Fact]
    public async Task SpaRoutes_WhenWwwrootIndexExists_ServeIndexHtmlWhileApi404StaysJson()
    {
        var tempWebRoot = Path.Combine(Path.GetTempPath(), $"tcg-wwwroot-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempWebRoot);
        try
        {
            const string markerHtml = "<!doctype html><html><body><div id=\"root\">TCG-SPA</div></body></html>";
            await File.WriteAllTextAsync(Path.Combine(tempWebRoot, "index.html"), markerHtml);

            using var spaFactory = factory.WithWebHostBuilder(builder => builder.UseWebRoot(tempWebRoot));
            using var client = spaFactory.CreateClient();

            var rootRes = await client.GetAsync("/");
            Assert.Equal(HttpStatusCode.OK, rootRes.StatusCode);
            Assert.Equal("text/html", rootRes.Content.Headers.ContentType?.MediaType);
            Assert.Contains("TCG-SPA", await rootRes.Content.ReadAsStringAsync());

            var decksRes = await client.GetAsync("/decks");
            Assert.Equal(HttpStatusCode.OK, decksRes.StatusCode);
            Assert.Equal("text/html", decksRes.Content.Headers.ContentType?.MediaType);
            Assert.Contains("TCG-SPA", await decksRes.Content.ReadAsStringAsync());

            var healthRes = await client.GetAsync("/api/health");
            Assert.Equal(HttpStatusCode.OK, healthRes.StatusCode);

            var missingApiRes = await client.GetAsync("/api/missing-endpoint");
            Assert.Equal(HttpStatusCode.NotFound, missingApiRes.StatusCode);
            Assert.Equal("application/problem+json", missingApiRes.Content.Headers.ContentType?.MediaType);
        }
        finally
        {
            if (Directory.Exists(tempWebRoot))
            {
                Directory.Delete(tempWebRoot, recursive: true);
            }
        }
    }
}
