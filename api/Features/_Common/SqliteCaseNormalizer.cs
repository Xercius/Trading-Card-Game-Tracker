namespace api.Features._Common;

internal static class SqliteCaseNormalizer
{
    internal static string Normalize(string value)
    {
        var trimmed = value.Trim();
        if (trimmed.Length == 0)
        {
            return trimmed;
        }

        char[]? buffer = null;
        for (var i = 0; i < trimmed.Length; i++)
        {
            var c = trimmed[i];
            if (c >= 'A' && c <= 'Z')
            {
                buffer ??= trimmed.ToCharArray();
                buffer[i] = (char)(c + ('a' - 'A'));
            }
        }

        return buffer is null ? trimmed : new string(buffer);
    }
}
