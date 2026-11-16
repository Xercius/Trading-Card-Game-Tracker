using System.Text.Json;
using System.Text.RegularExpressions;
using api.Models;

namespace api.Features.Decks.Services;

/// <summary>
/// Extracts Magic: The Gathering rules metadata (color identity, commander eligibility,
/// basic land status, and per-card copy limits) from <see cref="Card"/> records.
/// </summary>
public static partial class MtgCardMetadataHelper
{
    private static readonly Dictionary<string, int> NumberWords = new(StringComparer.OrdinalIgnoreCase)
    {
        ["one"] = 1,
        ["two"] = 2,
        ["three"] = 3,
        ["four"] = 4,
        ["five"] = 5,
        ["six"] = 6,
        ["seven"] = 7,
        ["eight"] = 8,
        ["nine"] = 9,
        ["ten"] = 10,
        ["eleven"] = 11,
        ["twelve"] = 12,
        ["thirteen"] = 13,
        ["fourteen"] = 14,
        ["fifteen"] = 15,
        ["sixteen"] = 16,
        ["seventeen"] = 17,
        ["eighteen"] = 18,
        ["nineteen"] = 19,
        ["twenty"] = 20
    };

    [GeneratedRegex(@"a deck can have up to (\w+) cards named", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex UpToCopiesRegex();

    /// <summary>
    /// Reads the Scryfall <c>colorIdentity</c> array from <c>Card.ExtraCardDataJson</c>.
    /// </summary>
    public static IReadOnlyList<string> GetColorIdentity(Card? card)
    {
        if (card is null || string.IsNullOrWhiteSpace(card.ExtraCardDataJson))
        {
            return [];
        }

        try
        {
            using var doc = JsonDocument.Parse(card.ExtraCardDataJson);
            if (doc.RootElement.ValueKind == JsonValueKind.Object &&
                doc.RootElement.TryGetProperty("colorIdentity", out var prop) &&
                prop.ValueKind == JsonValueKind.Array)
            {
                var colors = new List<string>();
                foreach (var item in prop.EnumerateArray())
                {
                    if (item.ValueKind == JsonValueKind.String)
                    {
                        var val = item.GetString()?.Trim().ToUpperInvariant();
                        if (!string.IsNullOrEmpty(val) && !colors.Contains(val, StringComparer.OrdinalIgnoreCase))
                        {
                            colors.Add(val);
                        }
                    }
                }
                return colors;
            }
        }
        catch (JsonException)
        {
        }

        return [];
    }

    /// <summary>
    /// Returns true if <paramref name="cardIdentity"/> contains only colors present in <paramref name="commanderIdentity"/>.
    /// </summary>
    public static bool IsColorIdentitySubset(
        IReadOnlyCollection<string> cardIdentity,
        IReadOnlyCollection<string> commanderIdentity)
    {
        if (cardIdentity.Count == 0)
        {
            return true;
        }

        var allowed = new HashSet<string>(commanderIdentity, StringComparer.OrdinalIgnoreCase);
        return cardIdentity.All(allowed.Contains);
    }

    /// <summary>
    /// Returns true if the card can be designated as a Commander (front-face Legendary Creature
    /// or rules text containing "can be your commander").
    /// </summary>
    public static bool CanBeCommander(Card? card)
    {
        if (card is null)
        {
            return false;
        }

        var rulesText = GetCombinedRulesText(card);
        if (!string.IsNullOrWhiteSpace(rulesText) &&
            rulesText.Contains("can be your commander", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var typeLine = GetFrontFaceTypeLine(card);
        if (!string.IsNullOrWhiteSpace(typeLine))
        {
            var tokens = GetLeftOfDashTokens(typeLine);
            return tokens.Contains("Legendary") && tokens.Contains("Creature");
        }

        if (card.CardTypes.Count > 0)
        {
            var typeNames = card.CardTypes
                .Select(ct => ct.CardType?.Name?.Trim())
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            return typeNames.Contains("Legendary") && typeNames.Contains("Creature");
        }

        return false;
    }

    /// <summary>
    /// Returns true if the card is a Basic Land.
    /// </summary>
    public static bool IsBasicLand(Card? card)
    {
        if (card is null)
        {
            return false;
        }

        var typeLine = GetFrontFaceTypeLine(card);
        if (!string.IsNullOrWhiteSpace(typeLine))
        {
            var tokens = GetLeftOfDashTokens(typeLine);
            return tokens.Contains("Basic") && tokens.Contains("Land");
        }

        if (card.CardTypes.Count > 0)
        {
            var typeNames = card.CardTypes
                .Select(ct => ct.CardType?.Name?.Trim())
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            return typeNames.Contains("Basic") && typeNames.Contains("Land");
        }

        return false;
    }

    /// <summary>
    /// Returns the maximum allowed copies for <paramref name="card"/>, or <c>null</c> if unlimited.
    /// </summary>
    public static int? GetMaxAllowedCopies(Card? card, int defaultLimit)
    {
        if (IsBasicLand(card))
        {
            return null;
        }

        var rulesText = GetCombinedRulesText(card);
        if (!string.IsNullOrWhiteSpace(rulesText))
        {
            if (rulesText.Contains("a deck can have any number of cards named", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            var match = UpToCopiesRegex().Match(rulesText);
            if (match.Success)
            {
                var token = match.Groups[1].Value;
                if (int.TryParse(token, out var numericLimit) && numericLimit > 0)
                {
                    return numericLimit;
                }
                if (NumberWords.TryGetValue(token, out var wordLimit))
                {
                    return wordLimit;
                }
            }
        }

        return defaultLimit;
    }

    private static string? GetFrontFaceTypeLine(Card card)
    {
        if (string.IsNullOrWhiteSpace(card.ExtraCardDataJson))
        {
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(card.ExtraCardDataJson);
            if (doc.RootElement.ValueKind == JsonValueKind.Object &&
                doc.RootElement.TryGetProperty("typeLine", out var prop) &&
                prop.ValueKind == JsonValueKind.String)
            {
                var full = prop.GetString();
                if (string.IsNullOrWhiteSpace(full))
                {
                    return null;
                }
                var splitIdx = full.IndexOf("//", StringComparison.Ordinal);
                return splitIdx >= 0 ? full[..splitIdx].Trim() : full.Trim();
            }
        }
        catch (JsonException)
        {
        }

        return null;
    }

    private static HashSet<string> GetLeftOfDashTokens(string typeLine)
    {
        var dashIdx = typeLine.IndexOf('—');
        var left = dashIdx >= 0 ? typeLine[..dashIdx] : typeLine;
        return left
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private static string? GetCombinedRulesText(Card? card)
    {
        if (card is null) return null;
        if (!string.IsNullOrWhiteSpace(card.Rules) && !string.IsNullOrWhiteSpace(card.Text))
        {
            return $"{card.Rules}\n{card.Text}";
        }
        return !string.IsNullOrWhiteSpace(card.Rules) ? card.Rules : card.Text;
    }
}
