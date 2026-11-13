using api.Models;

namespace api.Importing.Swccg;

/// <summary>
/// Maintains existing printing identities and resolves reprint/variant relationships for SWCCG cards.
/// </summary>
internal sealed class SwccgSharedIdentity
{
    private readonly Dictionary<string, CardPrinting> _byUid = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<(int SetId, string Number), CardPrinting> _bySetNumber = new();

    internal SwccgSharedIdentity(IEnumerable<CardPrinting> existingPrintings)
    {
        foreach (var printing in existingPrintings)
        {
            Register(printing);
        }
    }

    internal void Register(CardPrinting printing)
    {
        if (!string.IsNullOrWhiteSpace(printing.CardUid))
        {
            _byUid[printing.CardUid.Trim()] = printing;
        }

        if (printing.SetId.HasValue && !string.IsNullOrWhiteSpace(printing.Number))
        {
            _bySetNumber[(printing.SetId.Value, printing.Number.Trim())] = printing;
        }
    }

    internal CardPrinting? FindPrinting(string? cardUid, Set? set, string? number)
    {
        if (!string.IsNullOrWhiteSpace(cardUid) && _byUid.TryGetValue(cardUid.Trim(), out var byUid))
        {
            return byUid;
        }

        if (set is not null &&
            !string.IsNullOrWhiteSpace(number) &&
            _bySetNumber.TryGetValue((set.Id, number.Trim()), out var bySetNum))
        {
            return bySetNum;
        }

        return null;
    }
}

