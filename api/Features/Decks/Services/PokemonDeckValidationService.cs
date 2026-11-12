using System.Text.Json;
using api.Features.Decks.Dtos;
using api.Models;

namespace api.Features.Decks.Services;

public sealed class PokemonDeckValidationService
{
    private const int StandardDeckSize = 60;
    private const int MaxCopiesPerName = 4;

    /// <summary>
    /// Validates Pokémon deck cards against the selected supported format.
    /// </summary>
    public DeckValidationResponse Validate(string? format, IReadOnlyCollection<DeckCard> deckCards)
    {
        var normalizedFormat = string.IsNullOrWhiteSpace(format) ? "Standard" : format.Trim();
        var standard = string.Equals(normalizedFormat, "Standard", StringComparison.OrdinalIgnoreCase);
        var cards = deckCards.Where(card => card.QuantityInDeck > 0).ToList();
        var cardCount = cards.Sum(card => (long)card.QuantityInDeck);
        var basicPokemonCount = cards
            .Where(card => IsBasicPokemon(card.CardPrinting?.Card))
            .Sum(card => (long)card.QuantityInDeck);
        var violations = new List<string>();

        if (!standard)
        {
            violations.Add($"Pokémon format \"{normalizedFormat}\" is not supported.");
        }
        else
        {
            AddDeckSizeViolation(cardCount, violations);
            if (basicPokemonCount == 0)
            {
                violations.Add("Standard decks must contain at least one Basic Pokémon.");
            }
            AddCopyLimitViolations(cards, violations);
            AddGlobalLimitViolation(
                cards,
                card => IsAceSpec(card.CardPrinting?.RarityLookup?.Name),
                "Standard decks may contain no more than 1 ACE SPEC card",
                violations);
            AddGlobalLimitViolation(
                cards,
                card => IsRadiantPokemon(card.CardPrinting?.Card),
                "Standard decks may contain no more than 1 Radiant Pokémon",
                violations);
        }

        return new DeckValidationResponse(normalizedFormat, violations.Count == 0, cardCount, basicPokemonCount, violations);
    }

    private static void AddDeckSizeViolation(long cardCount, ICollection<string> violations)
    {
        if (cardCount != StandardDeckSize)
        {
            violations.Add($"Standard decks must contain exactly 60 cards (currently {cardCount}).");
        }
    }

    private static void AddCopyLimitViolations(IEnumerable<DeckCard> cards, ICollection<string> violations)
    {
        foreach (var group in cards
            .Where(card => !IsBasicEnergy(card.CardPrinting?.Card))
            .GroupBy(card => card.CardPrinting?.Card?.Name ?? string.Empty, StringComparer.Ordinal))
        {
            var nameCount = group.Sum(card => (long)card.QuantityInDeck);
            if (nameCount > MaxCopiesPerName)
            {
                violations.Add($"\"{group.Key}\" exceeds the limit of 4 copies ({nameCount} present).");
            }
        }
    }

    private static void AddGlobalLimitViolation(
        IEnumerable<DeckCard> cards,
        Func<DeckCard, bool> matches,
        string message,
        ICollection<string> violations)
    {
        var count = cards.Where(matches)
            .Sum(card => (long)card.QuantityInDeck);
        if (count > 1)
        {
            violations.Add($"{message} ({count} present).");
        }
    }

    private static bool IsBasicPokemon(Card? card) =>
        card is not null &&
        string.Equals(card.Stage?.Trim(), "Basic", StringComparison.OrdinalIgnoreCase) &&
        card.CardTypes.Any(type => string.Equals(type.CardType?.Name?.Trim(), "Pokemon", StringComparison.OrdinalIgnoreCase));

    private static bool IsBasicEnergy(Card? card)
    {
        if (card is null ||
            !card.CardTypes.Any(type => string.Equals(type.CardType?.Name?.Trim(), "Energy", StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        if (card.CardSubTypes.Any(type =>
            string.Equals(type.SubType?.Name?.Trim(), "Basic", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(type.SubType?.Name?.Trim(), "Basic Energy", StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        return TryReadMetadata(card.ExtraCardDataJson, "energyType", out var energyType) &&
            (string.Equals(energyType, "Basic", StringComparison.OrdinalIgnoreCase) ||
             string.Equals(energyType, "Basic Energy", StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsAceSpec(string? rarity) =>
        string.Equals(rarity?.Trim(), "Ace Spec Rare", StringComparison.OrdinalIgnoreCase);

    private static bool IsRadiantPokemon(Card? card) =>
        card is not null &&
        card.CardTypes.Any(type =>
            string.Equals(type.CardType?.Name?.Trim(), "Pokemon", StringComparison.OrdinalIgnoreCase)) &&
        card.Name.StartsWith("Radiant ", StringComparison.OrdinalIgnoreCase);

    private static bool TryReadMetadata(string? json, string propertyName, out string? value)
    {
        value = null;
        if (string.IsNullOrWhiteSpace(json)) return false;

        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind == JsonValueKind.Object &&
                document.RootElement.TryGetProperty(propertyName, out var property) &&
                property.ValueKind == JsonValueKind.String)
            {
                value = property.GetString();
                return !string.IsNullOrWhiteSpace(value);
            }
        }
        catch (JsonException)
        {
        }

        return false;
    }
}
