using System;
using System.Collections.Generic;

namespace api.Shared;

public static class CsvUtils
{
    public static IReadOnlyList<string> Parse(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Array.Empty<string>();
        }

        var segments = value.Split(',', StringSplitOptions.RemoveEmptyEntries);

        var seen = new HashSet<string>(StringComparer.Ordinal);
        var results = new List<string>(segments.Length);

        foreach (var segment in segments)
        {
            var normalized = segment.Trim();
            if (normalized.Length == 0)
            {
                continue;
            }

            if (seen.Add(normalized))
            {
                results.Add(normalized);
            }
        }

        return results.Count == 0
            ? Array.Empty<string>()
            : results;
    }
}
