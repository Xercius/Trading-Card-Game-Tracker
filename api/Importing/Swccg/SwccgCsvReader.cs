using System.Text;

namespace api.Importing.Swccg;

/// <summary>
/// Reads SWCCG CSV streams with strict UTF-8 to Latin-1 fallback and parses RFC-4180 records by header name.
/// </summary>
internal static class SwccgCsvReader
{
    private const int MaxCsvBytes = 10 * 1024 * 1024;
    private const int MaxRowCount = 50_000;

    internal static List<(int RowNumber, Dictionary<string, string> Fields)> ReadRows(Stream stream)
    {
        var bytes = ReadBoundedBytes(stream);
        var text = DecodeText(bytes);
        var parsed = ParseCsv(text);
        if (parsed.Count == 0)
            return [];

        var headers = parsed[0].Select(h => h.Trim()).ToArray();
        var results = new List<(int, Dictionary<string, string>)>(Math.Max(0, parsed.Count - 1));

        for (var i = 1; i < parsed.Count; i++)
        {
            var values = parsed[i];
            if (values.All(string.IsNullOrWhiteSpace))
                continue;

            var row = new Dictionary<string, string>(headers.Length, StringComparer.OrdinalIgnoreCase);
            for (var col = 0; col < headers.Length; col++)
            {
                var header = headers[col];
                if (string.IsNullOrEmpty(header))
                    continue;
                row[header] = col < values.Count ? values[col].Trim() : string.Empty;
            }

            results.Add((i + 1, row));
        }

        return results;
    }

    internal static List<string> FindClosestMatches(
        string query,
        IEnumerable<string> candidates,
        int maxResults = 3)
    {
        var normalizedQuery = SwccgCatalog.NormalizeForRelaxedMatch(query);
        if (string.IsNullOrEmpty(normalizedQuery))
            return [];

        return candidates
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(candidate =>
            {
                var normalizedCandidate = SwccgCatalog.NormalizeForRelaxedMatch(candidate);
                var containsBonus = normalizedCandidate.Contains(normalizedQuery, StringComparison.Ordinal)
                    || normalizedQuery.Contains(normalizedCandidate, StringComparison.Ordinal)
                    ? -5
                    : 0;
                var distance = ComputeLevenshtein(normalizedQuery, normalizedCandidate) + containsBonus;
                return (Candidate: candidate, Distance: distance);
            })
            .Where(x => x.Distance <= Math.Max(4, normalizedQuery.Length / 2))
            .OrderBy(x => x.Distance)
            .ThenBy(x => x.Candidate, StringComparer.OrdinalIgnoreCase)
            .Take(maxResults)
            .Select(x => x.Candidate)
            .ToList();
    }

    private static byte[] ReadBoundedBytes(Stream stream)
    {
        using var ms = new MemoryStream();
        var buffer = new byte[16 * 1024];
        int read;
        while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
        {
            if (ms.Length + read > MaxCsvBytes)
                throw new InvalidOperationException($"CSV stream exceeds maximum size of {MaxCsvBytes} bytes.");
            ms.Write(buffer, 0, read);
        }

        return ms.ToArray();
    }

    private static string DecodeText(byte[] bytes)
    {
        string text;
        try
        {
            var strictUtf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
            text = strictUtf8.GetString(bytes);
        }
        catch (DecoderFallbackException)
        {
            text = Encoding.Latin1.GetString(bytes);
        }

        return text.TrimStart('\uFEFF');
    }

    private static List<List<string>> ParseCsv(string text)
    {
        var rows = new List<List<string>>();
        var row = new List<string>();
        var field = new StringBuilder();
        var inQuotes = false;

        for (var i = 0; i < text.Length; i++)
        {
            var ch = text[i];
            if (inQuotes)
            {
                if (ch == '"' && i + 1 < text.Length && text[i + 1] == '"')
                {
                    field.Append('"');
                    i++;
                }
                else if (ch == '"')
                {
                    inQuotes = false;
                }
                else
                {
                    field.Append(ch);
                }
                continue;
            }

            if (ch == '"')
            {
                inQuotes = true;
            }
            else if (ch == ',')
            {
                row.Add(field.ToString());
                field.Clear();
            }
            else if (ch is '\r' or '\n')
            {
                if (ch == '\r' && i + 1 < text.Length && text[i + 1] == '\n')
                    i++;
                row.Add(field.ToString());
                rows.Add(row);
                if (rows.Count > MaxRowCount)
                    throw new InvalidOperationException($"CSV exceeds maximum row count of {MaxRowCount}.");
                row = new List<string>();
                field.Clear();
            }
            else
            {
                field.Append(ch);
            }
        }

        if (field.Length > 0 || row.Count > 0)
        {
            row.Add(field.ToString());
            rows.Add(row);
        }

        return rows;
    }

    private static int ComputeLevenshtein(string a, string b)
    {
        if (a.Length == 0) return b.Length;
        if (b.Length == 0) return a.Length;

        var prev = new int[b.Length + 1];
        var curr = new int[b.Length + 1];
        for (var j = 0; j <= b.Length; j++) prev[j] = j;

        for (var i = 1; i <= a.Length; i++)
        {
            curr[0] = i;
            for (var j = 1; j <= b.Length; j++)
            {
                var cost = a[i - 1] == b[j - 1] ? 0 : 1;
                curr[j] = Math.Min(Math.Min(curr[j - 1] + 1, prev[j] + 1), prev[j - 1] + cost);
            }
            (prev, curr) = (curr, prev);
        }

        return prev[b.Length];
    }
}
