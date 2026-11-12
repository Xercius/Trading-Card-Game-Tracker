using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using api.Data;
using api.Models;
using Microsoft.EntityFrameworkCore;

namespace api.Importing.Pokemon;

/// <summary>
/// Maintains game-scoped lookups, pre-seeds aspects/treatments, and formats rules for Pokémon TCG.
/// </summary>
public sealed class PokemonCatalog
{
    public const string GameName = "Pokemon TCG";
    private readonly AppDbContext _db;
    private Game _game = null!;

    private readonly Dictionary<string, Set> _setsByCode = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Treatment> _treatmentsByName = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Rarity> _raritiesByName = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, CardType> _cardTypesByName = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, SubType> _subTypesByName = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Trait> _traitsByName = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Aspect> _aspectsByName = new(StringComparer.OrdinalIgnoreCase);

    private static readonly (string Name, string Color)[] KnownAspects =
    [
        ("Grass", "#78C850"),
        ("Fire", "#F08030"),
        ("Water", "#6890F0"),
        ("Lightning", "#F8D030"),
        ("Psychic", "#F85888"),
        ("Fighting", "#C03028"),
        ("Darkness", "#705848"),
        ("Metal", "#B8B8D0"),
        ("Colorless", "#A8A878"),
        ("Dragon", "#7038F8"),
        ("Fairy", "#EE99AC")
    ];

    private static readonly string[] KnownTreatments =
    [
        "Standard",
        "Reverse Holo",
        "Holo",
        "1st Edition"
    ];

    public PokemonCatalog(AppDbContext db)
    {
        _db = db;
    }

    public Game Game => _game;

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

        // 1. Seed & Load Aspects
        var existingAspects = await _db.Aspects.Where(a => a.GameId == _game.Id).ToListAsync(ct);
        foreach (var a in existingAspects)
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

        // 2. Seed & Load Treatments
        var existingTreatments = await _db.Treatments.Where(t => t.GameId == _game.Id).ToListAsync(ct);
        foreach (var t in existingTreatments)
        {
            _treatmentsByName[t.Name.Trim()] = t;
        }

        foreach (var treatName in KnownTreatments)
        {
            if (!_treatmentsByName.TryGetValue(treatName, out var treatment))
            {
                treatment = new Treatment { GameId = _game.Id, Name = treatName };
                _db.Treatments.Add(treatment);
                _treatmentsByName[treatName] = treatment;
            }
        }

        // 3. Load Sets
        var existingSets = await _db.Sets.Where(s => s.GameId == _game.Id).ToListAsync(ct);
        foreach (var s in existingSets)
        {
            if (!string.IsNullOrWhiteSpace(s.Code))
            {
                _setsByCode[s.Code.Trim()] = s;
            }
        }

        // 4. Load Rarities
        var existingRarities = await _db.Rarities.Where(r => r.GameId == _game.Id).ToListAsync(ct);
        foreach (var r in existingRarities)
        {
            _raritiesByName[r.Name.Trim()] = r;
        }

        // 5. Load CardTypes
        var existingTypes = await _db.CardTypes.Where(ctp => ctp.GameId == _game.Id).ToListAsync(ct);
        foreach (var ctp in existingTypes)
        {
            _cardTypesByName[ctp.Name.Trim()] = ctp;
        }

        // 6. Load SubTypes
        var existingSubTypes = await _db.SubTypes.Where(st => st.GameId == _game.Id).ToListAsync(ct);
        foreach (var st in existingSubTypes)
        {
            _subTypesByName[st.Name.Trim()] = st;
        }

        // 7. Load Traits
        var existingTraits = await _db.Traits.Where(tr => tr.GameId == _game.Id).ToListAsync(ct);
        foreach (var tr in existingTraits)
        {
            _traitsByName[tr.Name.Trim()] = tr;
        }

        await _db.SaveChangesAsync(ct);
    }

    public Set? FindSetByCode(string code) =>
        _setsByCode.TryGetValue(code.Trim(), out var s) ? s : null;

    public void RegisterSet(Set set)
    {
        if (!string.IsNullOrWhiteSpace(set.Code))
        {
            _setsByCode[set.Code.Trim()] = set;
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

    public Aspect? FindAspect(string name) =>
        _aspectsByName.TryGetValue(name.Trim(), out var a) ? a : null;

    public Aspect GetOrCreateAspect(string name, string? fallbackColor = null)
    {
        var trimmed = name.Trim();
        if (!_aspectsByName.TryGetValue(trimmed, out var a))
        {
            a = new Aspect { GameId = _game.Id, Name = trimmed, Color = fallbackColor };
            _db.Aspects.Add(a);
            _aspectsByName[trimmed] = a;
        }
        return a;
    }

    public Rarity? GetOrCreateRarity(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;
        var trimmed = name.Trim();
        if (!_raritiesByName.TryGetValue(trimmed, out var r))
        {
            r = new Rarity { GameId = _game.Id, Name = trimmed };
            _db.Rarities.Add(r);
            _raritiesByName[trimmed] = r;
        }
        return r;
    }

    public CardType GetOrCreateCardType(string name)
    {
        var trimmed = name.Trim();
        if (!_cardTypesByName.TryGetValue(trimmed, out var ct))
        {
            ct = new CardType { GameId = _game.Id, Name = trimmed };
            _db.CardTypes.Add(ct);
            _cardTypesByName[trimmed] = ct;
        }
        return ct;
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

    public Trait GetOrCreateTrait(string name)
    {
        var trimmed = name.Trim();
        if (!_traitsByName.TryGetValue(trimmed, out var tr))
        {
            tr = new Trait { GameId = _game.Id, Name = trimmed };
            _db.Traits.Add(tr);
            _traitsByName[trimmed] = tr;
        }
        return tr;
    }

    public static string? NormalizeStage(string? stage)
    {
        if (string.IsNullOrWhiteSpace(stage)) return null;
        var trimmed = stage.Trim();
        if (string.Equals(trimmed, "Stage1", StringComparison.OrdinalIgnoreCase)) return "Stage 1";
        if (string.Equals(trimmed, "Stage2", StringComparison.OrdinalIgnoreCase)) return "Stage 2";
        return trimmed;
    }

    public static string? NormalizeSetLogoUrl(string? rawLogoUrl)
    {
        if (string.IsNullOrWhiteSpace(rawLogoUrl)) return null;
        var trimmed = rawLogoUrl.Trim().TrimEnd('/');
        if (trimmed.EndsWith(".webp", StringComparison.OrdinalIgnoreCase) ||
            trimmed.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
            trimmed.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
            trimmed.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase) ||
            trimmed.EndsWith(".svg", StringComparison.OrdinalIgnoreCase))
        {
            return trimmed;
        }
        return $"{trimmed}.webp";
    }

    public static string ResolvePrimaryCardType(string? category, string? trainerType)
    {
        var resolvedCategory = !string.IsNullOrWhiteSpace(category) ? category.Trim() : "Pokemon";
        if (string.Equals(resolvedCategory, "Trainer", StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(trainerType))
        {
            return trainerType.Trim();
        }
        return resolvedCategory;
    }

    public string ComputeMechanicalCardUid(TcgdexCard card)
    {
        var primaryType = ResolvePrimaryCardType(card.Category, card.TrainerType);
        var normalizedStage = NormalizeStage(card.Stage);
        var rulesText = BuildRulesText(card);
        return ComputeMechanicalCardUid(
            primaryType,
            card.Name,
            normalizedStage,
            card.Hp,
            card.Types,
            rulesText,
            card.EvolveFrom,
            card.Retreat,
            card.Weaknesses,
            card.Resistances);
    }

    public static string ComputeMechanicalCardUid(Card existingCard)
    {
        var primaryType = existingCard.CardTypes.FirstOrDefault(ct => ct.SourceRole == "Type")?.CardType?.Name
                          ?? existingCard.CardTypes.FirstOrDefault()?.CardType?.Name
                          ?? "Pokemon";
        var normalizedStage = NormalizeStage(existingCard.Stage);
        var types = existingCard.CardAspects
            .Select(ca => ca.Aspect?.Name)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name!);
        var mechanics = ReadMechanicalDetails(existingCard.ExtraCardDataJson);
        return ComputeMechanicalCardUid(
            primaryType,
            existingCard.Name,
            normalizedStage,
            existingCard.Hp,
            types,
            existingCard.Text,
            mechanics?.EvolveFrom,
            mechanics?.Retreat,
            mechanics?.Weaknesses,
            mechanics?.Resistances);
    }

    public static string ComputeMechanicalCardUid(
        string? primaryType,
        string name,
        string? normalizedStage,
        int? hp,
        IEnumerable<string>? types,
        string? rulesText,
        string? evolveFrom = null,
        int? retreat = null,
        IEnumerable<TcgdexWeakRes>? weaknesses = null,
        IEnumerable<TcgdexWeakRes>? resistances = null)
    {
        var normType = NormalizeIdentityPart(primaryType ?? "Pokemon");
        var normName = NormalizeIdentityPart(name);
        var normStage = NormalizeIdentityPart(normalizedStage);
        var normHp = hp.HasValue ? hp.Value.ToString() : string.Empty;
        var normTypes = types == null
            ? string.Empty
            : string.Join(",",
                types
                    .Where(t => !string.IsNullOrWhiteSpace(t))
                    .Select(NormalizeIdentityPart)
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(t => t, StringComparer.Ordinal));
        var normRules = NormalizeRulesTextForIdentity(rulesText);
        var normEvolution = NormalizeIdentityPart(evolveFrom);
        var normRetreat = retreat?.ToString() ?? string.Empty;
        var normWeaknesses = NormalizeWeakRes(weaknesses);
        var normResistances = NormalizeWeakRes(resistances);

        var fingerprint = $"{normType}|{normName}|{normStage}|{normHp}|{normTypes}|{normRules}|{normEvolution}|{normRetreat}|{normWeaknesses}|{normResistances}";
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(fingerprint));
        var hashHex = Convert.ToHexString(hashBytes, 0, 8).ToLowerInvariant();
        var slug = BuildCardSlug(name);

        return $"pkmn:{slug}:{hashHex}";
    }

    private static string NormalizeWeakRes(IEnumerable<TcgdexWeakRes>? entries)
    {
        return entries == null
            ? string.Empty
            : string.Join(",",
                entries
                    .Select(entry => $"{NormalizeIdentityPart(entry.Type)}:{NormalizeIdentityPart(entry.Value)}")
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(entry => entry, StringComparer.Ordinal));
    }

    private static MechanicalCardDetails? ReadMechanicalDetails(string? extraCardDataJson)
    {
        if (string.IsNullOrWhiteSpace(extraCardDataJson))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<MechanicalCardDetails>(
                extraCardDataJson,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (JsonException)
        {
            return new MechanicalCardDetails { EvolveFrom = extraCardDataJson };
        }
    }

    private sealed class MechanicalCardDetails
    {
        [JsonPropertyName("evolveFrom")]
        public string? EvolveFrom { get; set; }

        [JsonPropertyName("retreat")]
        public int? Retreat { get; set; }

        [JsonPropertyName("weaknesses")]
        public List<TcgdexWeakRes>? Weaknesses { get; set; }

        [JsonPropertyName("resistances")]
        public List<TcgdexWeakRes>? Resistances { get; set; }
    }

    private static string NormalizeIdentityPart(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        return Regex.Replace(value.Trim(), @"\s+", " ").ToLowerInvariant();
    }

    private static string NormalizeRulesTextForIdentity(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;
        var lines = text
            .Replace("\r\n", "\n")
            .Replace('\r', '\n')
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(line => Regex.Replace(line, @"\s+", " ").ToLowerInvariant());
        return string.Join("\n", lines);
    }

    private static string BuildCardSlug(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "card";
        var sb = new StringBuilder();
        bool lastWasHyphen = false;
        foreach (var ch in name.Trim().ToLowerInvariant())
        {
            if ((ch >= 'a' && ch <= 'z') || (ch >= '0' && ch <= '9'))
            {
                sb.Append(ch);
                lastWasHyphen = false;
                if (sb.Length >= 36) break;
            }
            else if (!lastWasHyphen && sb.Length > 0)
            {
                sb.Append('-');
                lastWasHyphen = true;
                if (sb.Length >= 36) break;
            }
        }
        var slug = sb.ToString().Trim('-');
        return string.IsNullOrEmpty(slug) ? "card" : slug;
    }

    public string BuildRulesText(TcgdexCard card)
    {
        var sb = new StringBuilder();

        if (card.Item != null && (!string.IsNullOrWhiteSpace(card.Item.Name) || !string.IsNullOrWhiteSpace(card.Item.Effect)))
        {
            var itemName = string.IsNullOrWhiteSpace(card.Item.Name) ? "Item" : card.Item.Name.Trim();
            var itemEffect = card.Item.Effect?.Trim();
            sb.AppendLine(string.IsNullOrWhiteSpace(itemEffect)
                ? $"[Held Item] {itemName}"
                : $"[Held Item] {itemName}: {itemEffect}");
            sb.AppendLine();
        }

        if (card.Abilities != null && card.Abilities.Count > 0)
        {
            foreach (var ab in card.Abilities)
            {
                var type = string.IsNullOrWhiteSpace(ab.Type) ? "Ability" : ab.Type.Trim();
                sb.AppendLine($"[{type}] {ab.Name}: {ab.Effect}");
                sb.AppendLine();
            }
        }

        if (card.Attacks != null && card.Attacks.Count > 0)
        {
            foreach (var atk in card.Attacks)
            {
                var cost = atk.Cost != null && atk.Cost.Count > 0 ? string.Join("", atk.Cost.Select(c => $"[{c}]")) : "";
                var dmg = !string.IsNullOrWhiteSpace(atk.Damage) ? $" ({atk.Damage})" : "";
                sb.Append($"{cost} {atk.Name}{dmg}");
                if (!string.IsNullOrWhiteSpace(atk.Effect))
                {
                    sb.Append($": {atk.Effect}");
                }
                sb.AppendLine();
            }
        }

        if (!string.IsNullOrWhiteSpace(card.Effect))
        {
            if (sb.Length > 0) sb.AppendLine();
            sb.AppendLine(card.Effect.Trim());
        }

        return sb.ToString().Trim();
    }

    public string BuildExtraCardDataJson(TcgdexCard card)
    {
        var payload = new
        {
            stage = card.Stage,
            suffix = card.Suffix,
            evolveFrom = card.EvolveFrom,
            dexId = card.DexId,
            retreat = card.Retreat,
            weaknesses = card.Weaknesses,
            resistances = card.Resistances,
            regulationMark = card.RegulationMark,
            legal = card.Legal,
            trainerType = card.TrainerType,
            energyType = card.EnergyType,
            category = card.Category,
            updated = card.Updated
        };

        return JsonSerializer.Serialize(payload, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        });
    }

    public string BuildPrintingDetailsJson(TcgdexCard card, TcgdexVariantDetailed? variantDetail = null)
    {
        var payload = new
        {
            tcgdexId = card.Id,
            localId = card.LocalId,
            regulationMark = card.RegulationMark,
            cameoDexIds = card.CameoDexIds,
            variantId = variantDetail?.VariantId,
            variantType = variantDetail?.Type,
            variantSubtype = variantDetail?.Subtype,
            variantSize = variantDetail?.Size,
            variantFoil = variantDetail?.Foil,
            variantStamps = variantDetail?.Stamp,
            thirdParty = variantDetail?.ThirdParty ?? card.ThirdParty
        };

        return JsonSerializer.Serialize(payload, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        });
    }
}
