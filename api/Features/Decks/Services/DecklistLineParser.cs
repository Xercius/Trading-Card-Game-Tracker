using System.Text.RegularExpressions;

namespace api.Features.Decks.Services;

/// <summary>
/// Parses plain-text decklists across MTG, Pokémon TCG, Star Wars: Unlimited, and Star Wars CCG
/// into structured line items with target zone metadata.
/// </summary>
public static partial class DecklistLineParser
{
    public sealed record ParsedDecklistLine(
        int LineNumber,
        string RawLine,
        int Quantity,
        string CardName,
        string? SetCode,
        string? CollectorNumber,
        string Zone,
        string? UnsplitName = null,
        string? ParseError = null);

    private static readonly Dictionary<string, string> SectionZoneMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["main"] = "main",
        ["main deck"] = "main",
        ["mainboard"] = "main",
        ["deck"] = "main",
        ["pokemon"] = "main",
        ["pokémon"] = "main",
        ["trainer"] = "main",
        ["trainers"] = "main",
        ["energy"] = "main",
        ["units"] = "main",
        ["unit"] = "main",
        ["events"] = "main",
        ["event"] = "main",
        ["upgrades"] = "main",
        ["upgrade"] = "main",
        ["creatures"] = "main",
        ["creature"] = "main",
        ["spells"] = "main",
        ["lands"] = "main",
        ["land"] = "main",
        ["artifacts"] = "main",
        ["enchantments"] = "main",
        ["planeswalkers"] = "main",
        ["instants"] = "main",
        ["sorceries"] = "main",
        ["characters"] = "main",
        ["starships"] = "main",
        ["vehicles"] = "main",
        ["weapons"] = "main",
        ["devices"] = "main",
        ["effects"] = "main",
        ["interrupts"] = "main",
        ["locations"] = "main",
        ["objectives"] = "main",
        ["objective"] = "main",
        ["admiral's orders"] = "main",
        ["admirals orders"] = "main",
        ["epic events"] = "main",
        ["sideboard"] = "sideboard",
        ["side"] = "sideboard",
        ["sb"] = "sideboard",
        ["commander"] = "commander",
        ["commanders"] = "commander",
        ["leader"] = "leader",
        ["leaders"] = "leader",
        ["base"] = "base",
        ["bases"] = "base",
        ["starting"] = "starting",
        ["starting cards"] = "starting",
        ["starting hand"] = "starting",
        ["defensive shields"] = "shields",
        ["defensive shield"] = "shields",
        ["shields"] = "shields",
        ["ideas"] = "ideas",
        ["idea"] = "ideas",
        ["maybeboard"] = "ideas",
        ["considering"] = "ideas",
    };

    [GeneratedRegex(@"^(?<qty>\d+)\s*[xX×]?\s+(?<rest>.+)$")]
    private static partial Regex QuantityPrefixRegex();

    [GeneratedRegex(@"^(?<name>.+?)\s*[\(\[](?<set>[A-Za-z0-9][A-Za-z0-9 _\-']{0,30})[\)\]]\s*(?:#?(?<num>[A-Za-z0-9][A-Za-z0-9\-/]*))?(?:\s+\*[FE]\*)?\s*$")]
    private static partial Regex BracketedSetRegex();

    [GeneratedRegex(@"^(?<name>.+?)\s+(?<set>[A-Z0-9]{2,6}(?:-[A-Z0-9]{1,4})?)\s+#?(?<num>\d+[A-Za-z]?)\s*$")]
    private static partial Regex TrailingSetNumberRegex();

    [GeneratedRegex(@"^(?<header>.+?)(?:\s*\(\d+\)|\s*[:\-]\s*\d+)?\s*:?\s*$")]
    private static partial Regex HeaderNormalizeRegex();

    /// <summary>Parses raw decklist text into structured card lines with zone assignments.</summary>
    public static IReadOnlyList<ParsedDecklistLine> Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        var results = new List<ParsedDecklistLine>();
        var currentZone = "main";
        var rawLines = text.Split(["\r\n", "\n", "\r"], StringSplitOptions.None);

        for (var i = 0; i < rawLines.Length; i++)
        {
            var raw = rawLines[i];
            var trimmed = raw.Trim();
            if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith("//", StringComparison.Ordinal))
            {
                continue;
            }

            if (trimmed.StartsWith('#'))
            {
                var afterHash = trimmed.TrimStart('#').Trim();
                if (TryResolveSectionHeader(afterHash, out var hashZone))
                {
                    currentZone = hashZone;
                }
                continue;
            }

            if (TryResolveSectionHeader(trimmed, out var zone))
            {
                currentZone = zone;
                continue;
            }

            results.Add(ParseCardLine(i + 1, raw, trimmed, currentZone));
        }

        return results;
    }

    private static bool TryResolveSectionHeader(string text, out string zone)
    {
        zone = "main";
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var match = HeaderNormalizeRegex().Match(text);
        if (!match.Success)
        {
            return false;
        }

        var candidate = match.Groups["header"].Value.Trim().TrimEnd(':').Trim();
        return SectionZoneMap.TryGetValue(candidate, out zone!);
    }

    private static ParsedDecklistLine ParseCardLine(int lineNumber, string rawLine, string trimmed, string currentZone)
    {
        var qtyMatch = QuantityPrefixRegex().Match(trimmed);
        int quantity;
        string remainder;

        if (qtyMatch.Success)
        {
            if (!int.TryParse(qtyMatch.Groups["qty"].Value, out quantity) || quantity <= 0)
            {
                return new ParsedDecklistLine(lineNumber, rawLine, 0, trimmed, null, null, currentZone, null, "Quantity must be a positive integer.");
            }
            remainder = qtyMatch.Groups["rest"].Value.Trim();
        }
        else if (currentZone is "commander" or "leader" or "base")
        {
            quantity = 1;
            remainder = trimmed;
        }
        else
        {
            return new ParsedDecklistLine(lineNumber, rawLine, 0, trimmed, null, null, currentZone, null, "Missing card quantity (expected e.g. '4 Card Name').");
        }

        var bracketMatch = BracketedSetRegex().Match(remainder);
        if (bracketMatch.Success)
        {
            var name = bracketMatch.Groups["name"].Value.Trim();
            var set = bracketMatch.Groups["set"].Value.Trim();
            var numGroup = bracketMatch.Groups["num"];
            var num = numGroup.Success && !string.IsNullOrWhiteSpace(numGroup.Value) ? numGroup.Value.Trim() : null;
            var unsplit = num is null ? remainder : null;
            return new ParsedDecklistLine(lineNumber, rawLine, quantity, name, set, num, currentZone, unsplit);
        }

        var trailingMatch = TrailingSetNumberRegex().Match(remainder);
        if (trailingMatch.Success)
        {
            var name = trailingMatch.Groups["name"].Value.Trim();
            var set = trailingMatch.Groups["set"].Value.Trim();
            var num = trailingMatch.Groups["num"].Value.Trim();
            return new ParsedDecklistLine(lineNumber, rawLine, quantity, name, set, num, currentZone, remainder);
        }

        return new ParsedDecklistLine(lineNumber, rawLine, quantity, remainder, null, null, currentZone);
    }
}
