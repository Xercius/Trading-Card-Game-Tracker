using Microsoft.VisualBasic.FileIO;
using System.Text.Json;

namespace api.Shared.Importing;

public sealed class FileParser
{
    public async Task<FileParseResult> ParseAsync(IFormFile file, int? limit = null, CancellationToken ct = default)
    {
        if (file is null || file.Length == 0)
        {
            throw new FileParserException("File required.");
        }

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        return extension switch
        {
            ".csv" => await ParseCsvAsync(file, ct),
            ".json" => await ParseJsonAsync(file, limit, ct),
            _ => throw new FileParserException("Unsupported file type. Expected .csv or .json."),
        };
    }

    private static async Task<FileParseResult> ParseCsvAsync(IFormFile file, CancellationToken ct)
    {
        await using var stream = file.OpenReadStream();
        var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, ct);
        buffer.Position = 0;

        using var parserStream = new MemoryStream(buffer.ToArray(), writable: false);
        using var parser = new TextFieldParser(parserStream)
        {
            TextFieldType = FieldType.Delimited,
            HasFieldsEnclosedInQuotes = true,
        };
        parser.SetDelimiters(",");

        if (parser.EndOfData)
        {
            throw new FileParserException("Empty CSV file.");
        }

        var header = parser.ReadFields() ?? Array.Empty<string>();
        var columns = header.Select(h => h.Trim().ToLowerInvariant()).ToHashSet();
        string[] required = ["name", "set", "number"];
        var missing = required.Where(r => !columns.Contains(r)).ToArray();
        if (missing.Length > 0)
        {
            throw new FileParserException("CSV missing required columns.", new Dictionary<string, string[]>
            {
                ["missing"] = missing,
            });
        }

        buffer.Position = 0;
        return new FileParseResult(buffer, "text/csv");
    }

    private static async Task<FileParseResult> ParseJsonAsync(IFormFile file, int? limit, CancellationToken ct)
    {
        await using var stream = file.OpenReadStream();
        var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, ct);
        buffer.Position = 0;

        using var parserStream = new MemoryStream(buffer.ToArray(), writable: false);
        using var document = await JsonDocument.ParseAsync(parserStream, cancellationToken: ct);
        if (document.RootElement.ValueKind != JsonValueKind.Array)
        {
            throw new FileParserException("Top-level JSON must be an array.");
        }

        var effectiveLimit = limit ?? ImportingOptions.DefaultPreviewLimit;
        var index = 0;
        foreach (var element in document.RootElement.EnumerateArray())
        {
            if (index++ >= effectiveLimit) break;
            if (element.ValueKind != JsonValueKind.Object)
            {
                throw new FileParserException($"Item {index} is not an object.");
            }

            bool HasString(string key) =>
                element.TryGetProperty(key, out var value) &&
                value.ValueKind == JsonValueKind.String &&
                !string.IsNullOrWhiteSpace(value.GetString());

            if (!(HasString("set") || HasString("set_code")))
            {
                throw new FileParserException($"Item {index} missing 'set' or 'set_code'.");
            }

            if (!(HasString("number") || HasString("collector_number")))
            {
                throw new FileParserException($"Item {index} missing 'number' or 'collector_number'.");
            }

            if (!HasString("name"))
            {
                throw new FileParserException($"Item {index} missing 'name'.");
            }
        }

        buffer.Position = 0;
        return new FileParseResult(buffer, "application/json");
    }
}

/// <summary>
/// Represents the parsed upload buffer. The caller is responsible for disposing the instance
/// after consuming the <see cref="Stream"/>.
/// </summary>
public sealed class FileParseResult : IDisposable
{
    public FileParseResult(Stream stream, string contentType)
    {
        Stream = stream;
        ContentType = contentType;
    }

    public Stream Stream { get; }

    public string ContentType { get; }

    public Stream OpenRead()
    {
        Stream.Position = 0;
        return Stream;
    }

    public void Dispose() => Stream.Dispose();
}

public sealed class FileParserException : Exception
{
    public FileParserException(string message, IReadOnlyDictionary<string, string[]>? errors = null)
        : base(message)
    {
        Errors = errors;
    }

    public IReadOnlyDictionary<string, string[]>? Errors { get; }
}
