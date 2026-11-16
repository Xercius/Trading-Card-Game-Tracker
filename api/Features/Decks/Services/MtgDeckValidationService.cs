using api.Features.Decks.Dtos;
using api.Models;

namespace api.Features.Decks.Services;

public sealed class MtgDeckValidationService
{
    private const int ConstructedMinDeckSize = 60;
    private const int ConstructedMaxSideboardSize = 15;
    private const int ConstructedDefaultMaxCopies = 4;
    private const int CommanderMinDeckSize = 100;
    private const int CommanderDefaultMaxCopies = 1;

    /// <summary>
    /// Validates Magic: The Gathering deck cards against Constructed or Commander rules.
    /// </summary>
    public MtgDeckValidationResponse Validate(string? format, IReadOnlyCollection<DeckCard> deckCards)
    {
        var normalizedFormat = string.IsNullOrWhiteSpace(format) ? "Constructed" : format.Trim();
        var activeCards = deckCards
            .Where(c => c.QuantityInDeck > 0 || c.QuantitySideboard > 0)
            .ToList();

        var mainDeckCount = activeCards.Sum(c => (long)c.QuantityInDeck);
        var sideboardCount = activeCards.Sum(c => (long)c.QuantitySideboard);
        var commanderCards = activeCards.Where(c => c.IsCommander && c.QuantityInDeck > 0).ToList();
        var commanderCount = commanderCards.Sum(c => (long)c.QuantityInDeck);

        IReadOnlyList<string> commanderIdentity = commanderCount == 1
            ? MtgCardMetadataHelper.GetColorIdentity(commanderCards[0].CardPrinting?.Card)
            : [];

        var violations = new List<string>();

        if (string.Equals(normalizedFormat, "Constructed", StringComparison.OrdinalIgnoreCase))
        {
            ValidateConstructed(activeCards, mainDeckCount, sideboardCount, violations);
        }
        else if (string.Equals(normalizedFormat, "Commander", StringComparison.OrdinalIgnoreCase))
        {
            ValidateCommander(activeCards, mainDeckCount, sideboardCount, commanderCards, commanderCount, commanderIdentity, violations);
        }
        else
        {
            violations.Add($"Magic: The Gathering format \"{normalizedFormat}\" is not supported.");
        }

        return new MtgDeckValidationResponse(
            normalizedFormat,
            violations.Count == 0,
            mainDeckCount,
            sideboardCount,
            commanderCount,
            commanderIdentity,
            violations);
    }

    private static void ValidateConstructed(
        IReadOnlyCollection<DeckCard> activeCards,
        long mainDeckCount,
        long sideboardCount,
        ICollection<string> violations)
    {
        if (mainDeckCount < ConstructedMinDeckSize)
        {
            violations.Add($"Constructed decks must contain at least {ConstructedMinDeckSize} cards in the main deck (currently {mainDeckCount}).");
        }

        if (sideboardCount > ConstructedMaxSideboardSize)
        {
            violations.Add($"Constructed sideboards may contain at most {ConstructedMaxSideboardSize} cards (currently {sideboardCount}).");
        }

        AddCopyLimitViolations(activeCards, ConstructedDefaultMaxCopies, includeSideboard: true, violations);
    }

    private static void ValidateCommander(
        IReadOnlyCollection<DeckCard> activeCards,
        long mainDeckCount,
        long sideboardCount,
        IReadOnlyList<DeckCard> commanderCards,
        long commanderCount,
        IReadOnlyList<string> commanderIdentity,
        ICollection<string> violations)
    {
        if (commanderCount != 1)
        {
            violations.Add($"Commander decks must have exactly 1 designated Commander (currently {commanderCount}).");
        }
        else
        {
            var commanderCard = commanderCards[0].CardPrinting?.Card;
            if (!MtgCardMetadataHelper.CanBeCommander(commanderCard))
            {
                var name = commanderCard?.Name ?? "Selected card";
                violations.Add($"\"{name}\" is not eligible to be a Commander.");
            }
        }

        if (sideboardCount > 0)
        {
            violations.Add($"Commander decks cannot have a sideboard (currently {sideboardCount} cards).");
        }

        if (mainDeckCount < CommanderMinDeckSize)
        {
            violations.Add($"Commander decks must contain at least {CommanderMinDeckSize} cards including the Commander (currently {mainDeckCount}).");
        }

        var mainCards = activeCards.Where(c => c.QuantityInDeck > 0).ToList();
        AddCopyLimitViolations(mainCards, CommanderDefaultMaxCopies, includeSideboard: false, violations);

        if (commanderCount == 1)
        {
            var allowedLabel = commanderIdentity.Count == 0 ? "Colorless" : string.Join("/", commanderIdentity);
            foreach (var dc in mainCards.Where(c => !c.IsCommander))
            {
                var card = dc.CardPrinting?.Card;
                var cardIdentity = MtgCardMetadataHelper.GetColorIdentity(card);
                if (!MtgCardMetadataHelper.IsColorIdentitySubset(cardIdentity, commanderIdentity))
                {
                    var cardName = card?.Name ?? "Unknown card";
                    var cardLabel = string.Join("/", cardIdentity);
                    violations.Add($"\"{cardName}\" ({cardLabel}) is outside the Commander's color identity ({allowedLabel}).");
                }
            }
        }
    }

    private static void AddCopyLimitViolations(
        IEnumerable<DeckCard> cards,
        int defaultLimit,
        bool includeSideboard,
        ICollection<string> violations)
    {
        foreach (var group in cards.GroupBy(c => c.CardPrinting?.Card?.Name ?? string.Empty, StringComparer.OrdinalIgnoreCase))
        {
            var sampleCard = group.First().CardPrinting?.Card;
            var maxAllowed = MtgCardMetadataHelper.GetMaxAllowedCopies(sampleCard, defaultLimit);
            if (!maxAllowed.HasValue)
            {
                continue;
            }

            var totalCount = group.Sum(c => (long)c.QuantityInDeck + (includeSideboard ? c.QuantitySideboard : 0));
            if (totalCount > maxAllowed.Value)
            {
                var copyLabel = maxAllowed.Value == 1 ? "copy" : "copies";
                violations.Add($"\"{group.Key}\" exceeds the limit of {maxAllowed.Value} {copyLabel} ({totalCount} present).");
            }
        }
    }
}
