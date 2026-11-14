using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using api.Data;
using api.Models;
using Microsoft.EntityFrameworkCore;

namespace api.Importing.Mtg;

/// <summary>
/// Maintains game-scoped lookups, pre-seeds MTG color aspects and treatments, and formats rules/metadata.
/// </summary>
public sealed class MtgCatalog
{
    public const string GameName = "Magic The Gathering";

    private static readonly JsonSerializerOptions CompactJsonOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private static readonly (string Name, string Color)[] KnownAspects =
    [
        ("White", "#F8F6D8"),
        ("Blue", "#0E68AB"),
        ("Black", "#150B00"),
        ("Red", "#D3202A"),
        ("Green", "#00733E"),
        ("Colorless", "#CAC5C0")
    ];

    private static readonly Dictionary<string, string> ColorCodeToAspectName = new(StringComparer.OrdinalIgnoreCase)
    {
        ["W"] = "White",
        ["U"] = "Blue",
        ["B"] = "Black",
        ["R"] = "Red",
        ["G"] = "Green",
        ["C"] = "Colorless"
    };

    private static readonly string[] KnownTreatments =
    [
        "Standard",
        "Foil",
        "Etched",
        "Showcase",
        "Borderless",
        "Extended Art",
        "Retro Frame",
        "Full Art"
    ];

    private readonly AppDbContext _db;
    private Game _game = null!;

    private readonly Dictionary<string, Set> _setsByCode = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Treatment> _treatmentsByName = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Rarity> _raritiesByName = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, CardType> _cardTypesByName = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, SubType> _subTypesByName = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Keyword> _keywordsByName = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Aspect> _aspectsByName = new(StringComparer.OrdinalIgnoreCase);

    public MtgCatalog(AppDbContext db)
    {
        _db = db;
    }

    public Game Game => _game;

    /// <summary>Loads or creates the MTG game row and pre-loads all game-scoped lookup tables.</summary>
    public async Task InitializeAsync(CancellationToken ct = default)
    {
        await _db.Games.LoadAsync(ct);
        _game = _db.Games.Local.SingleOrDefault(g => g.Name.Trim().Equals(GameName, StringComparison.OrdinalIgnoreCase))
                ?? new Game { Name = GameName };

        if (_db.Entry(_game).State == EntityState.Detached)
        {
            _db.Games.Add(_game);
            await _db.SaveChangesAsync(ct);
        }

        await LoadAndSeedAspectsAsync(ct);
        await LoadAndSeedTreatmentsAsync(ct);

        foreach (var s in await _db.Sets.Where(s => s.GameId == _game.Id).ToListAsync(ct))
        {
            if (!string.IsNullOrWhiteSpace(s.Code)) _setsByCode[s.Code.Trim()] = s;
        }
        foreach (var r in await _db.Rarities.Where(r => r.GameId == _game.Id).ToListAsync(ct))
        {
            _raritiesByName[r.Name.Trim()] = r;
        }
        foreach (var tp in await _db.CardTypes.Where(t => t.GameId == _game.Id).ToListAsync(ct))
        {
            _cardTypesByName[tp.Name.Trim()] = tp;
        }
        foreach (var st in await _db.SubTypes.Where(s => s.GameId == _game.Id).ToListAsync(ct))
        {
            _subTypesByName[st.Name.Trim()] = st;
        }
        foreach (var kw in await _db.Keywords.Where(k => k.GameId == _game.Id).ToListAsync(ct))
        {
            _keywordsByName[kw.Name.Trim()] = kw;
        }

        await _db.SaveChangesAsync(ct);
    }

    private async Task LoadAndSeedAspectsAsync(CancellationToken ct)
    {
        foreach (var a in await _db.Aspects.Where(a => a.GameId == _game.Id).ToListAsync(ct))
        {
            _aspectsByName[a.Name.Trim()] = a;
        }

        foreach (var (name, color) in KnownAspects)
        {
            if (!_aspectsByName.TryGetValue(name, out var aspect))
            {
                aspect = new Aspect { GameId = _game.Id, Name = name, Color = color };
                _db.Aspects.Add(aspect);
                _aspectsByName[name] = aspect;
            }
            else if (string.IsNullOrWhiteSpace(aspect.Color))
            {
                aspect.Color = color;
            }
        }
    }

    private async Task LoadAndSeedTreatmentsAsync(CancellationToken ct)
    {
        foreach (var t in await _db.Treatments.Where(t => t.GameId == _game.Id).ToListAsync(ct))
        {
            _treatmentsByName[t.Name.Trim()] = t;
        }

        foreach (var name in KnownTreatments)
        {
            if (!_treatmentsByName.TryGetValue(name, out var treatment))
            {
                treatment = new Treatment { GameId = _game.Id, Name = name };
                _db.Treatments.Add(treatment);
                _treatmentsByName[name] = treatment;
            }
        }
    }

    public Treatment GetOrCreateTreatment(string name)
    {
        var trimmed = name.Trim();
        if (!_treatmentsByName.TryGetValue(trimmed, out var t))
        {
            t = new Treatment { GameId = _game.Id, Name = trimmed };
            _db.Treatments.Add(t);
            _treatmentsByName[trimmed] = t;
        }
        return t;
    }

    public Rarity? GetOrCreateRarity(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;
        var formatted = CultureInfo.InvariantCulture.TextInfo.ToTitleCase(name.Trim().ToLowerInvariant());
        if (!_raritiesByName.TryGetValue(formatted, out var r))
        {
            r = new Rarity { GameId = _game.Id, Name = formatted };
            _db.Rarities.Add(r);
            _raritiesByName[formatted] = r;
        }
        return r;
    }

    public CardType GetOrCreateCardType(string name)
    {
        var trimmed = name.Trim();
        if (!_cardTypesByName.TryGetValue(trimmed, out var tp))
        {
            tp = new CardType { GameId = _game.Id, Name = trimmed };
            _db.CardTypes.Add(tp);
            _cardTypesByName[trimmed] = tp;
        }
        return tp;
    }

    public SubType GetOrCreateSubType(string name)
    {
        var trimmed = name.Trim();
        if (!_subTypesByName.TryGetValue(trimmed, out var st))
        {
            st = new SubType { GameId = _game.Id, Name = trimmed };
            _db.SubTypes.Add(st);
            _subTypesByName[trimmed] = st;
        }
        return st;
    }

    public Keyword GetOrCreateKeyword(string name)
    {
        var trimmed = name.Trim();
        if (!_keywordsByName.TryGetValue(trimmed, out var kw))
        {
            kw = new Keyword { GameId = _game.Id, Name = trimmed };
            _db.Keywords.Add(kw);
            _keywordsByName[trimmed] = kw;
        }
        return kw;
    }

    public IEnumerable<Aspect> ResolveAspects(ScryfallCard card)
    {
        var colorCodes = card.Colors is { Count: > 0 }
            ? card.Colors
            : card.CardFaces?.SelectMany(f => f.Colors ?? []).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

        if (colorCodes is null || colorCodes.Count == 0)
        {
            if (_aspectsByName.TryGetValue("Colorless", out var colorless))
            {
                yield return colorless;
            }
            yield break;
        }

        var yielded = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var code in colorCodes)
        {
            if (ColorCodeToAspectName.TryGetValue(code.Trim(), out var aspectName) &&
                yielded.Add(aspectName) &&
                _aspectsByName.TryGetValue(aspectName, out var aspect))
            {
                yield return aspect;
            }
        }
    }

    /// <summary>Splits Scryfall type_line (including multi-face "//") into CardTypes and SubTypes.</summary>
    public static (IReadOnlyList<string> Types, IReadOnlyList<string> SubTypes) ParseTypeLine(string? typeLine)
    {
        if (string.IsNullOrWhiteSpace(typeLine))
        {
            return ([], []);
        }

        var types = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var subTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var faceTypeLine in typeLine.Split("//", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var dashIdx = faceTypeLine.IndexOf('—');
            var left = dashIdx >= 0 ? faceTypeLine[..dashIdx] : faceTypeLine;
            var right = dashIdx >= 0 ? faceTypeLine[(dashIdx + 1)..] : string.Empty;

            foreach (var token in left.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                types.Add(token);
            }

            foreach (var token in right.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                subTypes.Add(token);
            }
        }

        return (types.ToList(), subTypes.ToList());
    }

    /// <summary>Maps Scryfall <c>set_type</c> to a clean UI accordion group label.</summary>
    public static string FormatSetTypeEra(string? setType)
    {
        var normalized = setType?.Trim().ToLowerInvariant() ?? string.Empty;
        return normalized switch
        {
            "core" or "expansion" => "Core & Expansions",
            "commander" => "Commander",
            "masters" => "Masters & Remastered",
            "draft_innovation" => "Draft Innovation",
            "promo" => "Promo",
            "masterpiece" => "Masterpieces",
            "funny" => "Un-Sets & Funny",
            "token" or "memorabilia" or "minigame" => "Tokens & Memorabilia",
            "alchemy" or "treasure_chest" or "duel_deck" or "from_the_vault" or "spellbook" or "premium_deck" or "arsenal" or "planechase" or "archenemy" or "vanguard" or "starter" or "box" => "Boxed & Supplemental",
            "" => "Other",
            _ => CultureInfo.InvariantCulture.TextInfo.ToTitleCase(normalized.Replace('_', ' '))
        };
    }

    /// <summary>Computes the canonical logical CardUid from Scryfall oracle_id or a deterministic fallback.</summary>
    public static string ComputeCanonicalCardUid(ScryfallCard card)
    {
        if (!string.IsNullOrWhiteSpace(card.OracleId))
        {
            return card.OracleId.Trim();
        }

        var faceOracleId = card.CardFaces?
            .Select(f => f.OracleId)
            .FirstOrDefault(id => !string.IsNullOrWhiteSpace(id));
        if (!string.IsNullOrWhiteSpace(faceOracleId))
        {
            return faceOracleId.Trim();
        }

        var raw = $"{card.Name.Trim()}|{card.TypeLine?.Trim()}|{card.OracleText?.Trim()}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        return $"mtg-{Convert.ToHexString(hash)[..32].ToLowerInvariant()}";
    }

    /// <summary>Builds rules text across single-faced and multi-faced Scryfall cards.</summary>
    public static string? BuildRulesText(ScryfallCard card)
    {
        if (!string.IsNullOrWhiteSpace(card.OracleText))
        {
            return card.OracleText.Trim();
        }

        if (card.CardFaces is { Count: > 0 })
        {
            var faceBlocks = card.CardFaces
                .Select(face =>
                {
                    var header = string.IsNullOrWhiteSpace(face.ManaCost)
                        ? face.Name
                        : $"{face.Name} {face.ManaCost}";
                    var lines = new[] { header, face.TypeLine, face.OracleText }
                        .Where(s => !string.IsNullOrWhiteSpace(s));
                    return string.Join("\n", lines);
                })
                .Where(block => !string.IsNullOrWhiteSpace(block));

            var combined = string.Join("\n//\n", faceBlocks);
            return string.IsNullOrWhiteSpace(combined) ? null : combined;
        }

        return null;
    }

    /// <summary>Serializes MTG-specific logical card attributes into <c>Card.ExtraCardDataJson</c>.</summary>
    public static string BuildExtraCardDataJson(ScryfallCard card)
    {
        var frontFace = card.CardFaces?.ElementAtOrDefault(0);
        var backFace = card.CardFaces?.ElementAtOrDefault(1);

        var payload = new
        {
            oracleId = card.OracleId,
            layout = card.Layout,
            manaCost = card.ManaCost ?? frontFace?.ManaCost,
            cmc = card.Cmc,
            typeLine = card.TypeLine,
            colors = card.Colors,
            colorIdentity = card.ColorIdentity,
            power = card.Power ?? frontFace?.Power,
            toughness = card.Toughness ?? frontFace?.Toughness,
            loyalty = card.Loyalty ?? frontFace?.Loyalty,
            defense = card.Defense ?? frontFace?.Defense,
            backManaCost = backFace?.ManaCost,
            backTypeLine = backFace?.TypeLine,
            backPower = backFace?.Power,
            backToughness = backFace?.Toughness,
            backLoyalty = backFace?.Loyalty
        };

        return JsonSerializer.Serialize(payload, CompactJsonOptions);
    }

    /// <summary>Serializes physical printing attributes into <c>CardPrinting.DetailsJson</c>.</summary>
    public static string BuildPrintingDetailsJson(ScryfallCard card, string finish, string treatmentName)
    {
        var payload = new
        {
            scryfallId = card.Id,
            finish,
            treatment = treatmentName,
            lang = card.Lang,
            frame = card.Frame,
            frameEffects = card.FrameEffects,
            borderColor = card.BorderColor,
            fullArt = card.FullArt,
            textless = card.Textless,
            promo = card.Promo,
            promoTypes = card.PromoTypes,
            variation = card.Variation,
            flavorText = card.FlavorText,
            printedName = card.PrintedName,
            printedTypeLine = card.PrintedTypeLine,
            printedText = card.PrintedText
        };

        return JsonSerializer.Serialize(payload, CompactJsonOptions);
    }

    /// <summary>Maps a Scryfall finish + frame attributes to the canonical Treatment name.</summary>
    public static string ResolveTreatmentName(ScryfallCard card, string finish)
    {
        if (string.Equals(finish, "etched", StringComparison.OrdinalIgnoreCase)) return "Etched";
        if (string.Equals(finish, "foil", StringComparison.OrdinalIgnoreCase)) return "Foil";

        if (card.FrameEffects is { Count: > 0 })
        {
            if (card.FrameEffects.Contains("showcase", StringComparer.OrdinalIgnoreCase)) return "Showcase";
            if (card.FrameEffects.Contains("extendedart", StringComparer.OrdinalIgnoreCase)) return "Extended Art";
        }

        if (string.Equals(card.BorderColor, "borderless", StringComparison.OrdinalIgnoreCase)) return "Borderless";
        if (card.Frame is "1993" or "1997") return "Retro Frame";
        if (card.FullArt) return "Full Art";

        return "Standard";
    }
}
