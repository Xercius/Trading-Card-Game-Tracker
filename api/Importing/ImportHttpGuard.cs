using System.Text;

namespace api.Importing;

/// <summary>
/// Provides SSRF URL validation and size-bounded HTTP response reading for catalog importers.
/// </summary>
internal static class ImportHttpGuard
{
    internal const long DefaultMaxJsonBytes = 32 * 1024 * 1024;
    internal const long DefaultMaxHtmlBytes = 4 * 1024 * 1024;

    /// <summary>Creates a primary handler that disables automatic HTTP redirects to prevent SSRF allowlist bypasses.</summary>
    internal static HttpClientHandler CreateNoRedirectHandler()
        => new() { AllowAutoRedirect = false };

    /// <summary>Throws if the HTTP response is a redirect (3xx), preventing unvalidated host redirection.</summary>
    internal static void EnsureNotRedirect(HttpResponseMessage response)
    {
        if ((int)response.StatusCode is >= 300 and < 400)
        {
            throw new InvalidOperationException(
                $"HTTP redirect ({(int)response.StatusCode}) is not permitted for importer requests.");
        }
    }

    /// <summary>Validates that a URL is absolute HTTPS and targets one of the allow-listed hosts.</summary>
    internal static bool TryValidateAllowedHttpsUri(
        string? rawUrl,
        IReadOnlySet<string> allowedHosts,
        out Uri? uri)
    {
        uri = null;
        if (string.IsNullOrWhiteSpace(rawUrl))
        {
            return false;
        }

        if (!Uri.TryCreate(rawUrl.Trim(), UriKind.Absolute, out var parsed) ||
            !string.Equals(parsed.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) ||
            !allowedHosts.Contains(parsed.Host))
        {
            return false;
        }

        uri = parsed;
        return true;
    }

    /// <summary>Returns the trimmed URL if it is a valid allow-listed HTTPS URL; otherwise null.</summary>
    internal static string? SanitizeAllowedImageUrl(string? rawUrl, IReadOnlySet<string> allowedHosts)
    {
        return TryValidateAllowedHttpsUri(rawUrl, allowedHosts, out var uri) && uri is not null
            ? uri.AbsoluteUri
            : null;
    }

    /// <summary>Fetches a response using <see cref="HttpCompletionOption.ResponseHeadersRead"/> and enforces a byte cap.</summary>
    internal static async Task<byte[]> GetBytesWithLimitAsync(
        HttpClient http,
        Uri requestUri,
        long maxBytes,
        CancellationToken ct)
    {
        using var response = await http.GetAsync(requestUri, HttpCompletionOption.ResponseHeadersRead, ct);
        EnsureNotRedirect(response);
        response.EnsureSuccessStatusCode();

        if (response.Content.Headers.ContentLength is { } contentLength && contentLength > maxBytes)
        {
            throw new InvalidOperationException($"Remote response exceeded maximum allowed size of {maxBytes} bytes.");
        }

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        return await ReadStreamWithLimitAsync(stream, maxBytes, ct);
    }

    /// <summary>Fetches UTF-8 text using <see cref="HttpCompletionOption.ResponseHeadersRead"/> and enforces a byte cap.</summary>
    internal static async Task<string> GetStringWithLimitAsync(
        HttpClient http,
        Uri requestUri,
        long maxBytes,
        CancellationToken ct)
    {
        var bytes = await GetBytesWithLimitAsync(http, requestUri, maxBytes, ct);
        return Encoding.UTF8.GetString(bytes);
    }

    /// <summary>Reads a stream into memory while enforcing a hard byte limit.</summary>
    internal static async Task<byte[]> ReadStreamWithLimitAsync(Stream source, long maxBytes, CancellationToken ct)
    {
        using var ms = new MemoryStream();
        var buffer = new byte[16 * 1024];
        long total = 0;

        while (true)
        {
            var read = await source.ReadAsync(buffer.AsMemory(0, buffer.Length), ct);
            if (read == 0)
            {
                break;
            }

            total += read;
            if (total > maxBytes)
            {
                throw new InvalidOperationException($"Stream exceeded maximum allowed size of {maxBytes} bytes.");
            }

            ms.Write(buffer, 0, read);
        }

        return ms.ToArray();
    }
}
