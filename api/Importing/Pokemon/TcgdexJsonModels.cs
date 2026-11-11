using System.Text.Json;
using System.Text.Json.Serialization;

namespace api.Importing.Pokemon;

public sealed class TcgdexCardCount
{
    public int Total { get; set; }
    public int Official { get; set; }
    public int? Normal { get; set; }
    public int? Reverse { get; set; }
    public int? Holo { get; set; }
    public int? FirstEd { get; set; }
}

public sealed class TcgdexSerieResume
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
}

public sealed class TcgdexSetResume
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Logo { get; set; }
    public string? Symbol { get; set; }
    public TcgdexCardCount? CardCount { get; set; }
}

public sealed class TcgdexCardStub
{
    public string Id { get; set; } = string.Empty;
    public string LocalId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Image { get; set; }
}

public sealed class TcgdexSetDetail
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Logo { get; set; }
    public string? Symbol { get; set; }
    public TcgdexSerieResume? Serie { get; set; }
    public string? ReleaseDate { get; set; }
    public TcgdexCardCount? CardCount { get; set; }
    public List<TcgdexCardStub>? Cards { get; set; }
}

public sealed class TcgdexVariants
{
    public bool Normal { get; set; }
    public bool Reverse { get; set; }
    public bool Holo { get; set; }
    public bool FirstEdition { get; set; }
    public bool WPromo { get; set; }
}

public sealed class TcgdexThirdParty
{
    public int? Cardmarket { get; set; }
    public int? Tcgplayer { get; set; }
    public int? Cardtrader { get; set; }
}

public sealed class TcgdexVariantDetailed
{
    public string? Type { get; set; }
    public string? Subtype { get; set; }
    public string? Size { get; set; }
    public List<string>? Stamp { get; set; }
    public string? Foil { get; set; }
    public string? VariantId { get; set; }
    public TcgdexThirdParty? ThirdParty { get; set; }
}

public sealed class TcgdexItem
{
    public string? Name { get; set; }
    public string? Effect { get; set; }
}

public sealed class TcgdexAttack
{
    public List<string>? Cost { get; set; }
    public string? Name { get; set; }
    public string? Effect { get; set; }
    [JsonConverter(typeof(FlexibleDamageConverter))]
    public string? Damage { get; set; }
}

public sealed class TcgdexAbility
{
    public string? Type { get; set; }
    public string? Name { get; set; }
    public string? Effect { get; set; }
}

public sealed class TcgdexWeakRes
{
    public string? Type { get; set; }
    public string? Value { get; set; }
}

public sealed class TcgdexLegalities
{
    public bool Standard { get; set; }
    public bool Expanded { get; set; }
}

public sealed class TcgdexCard
{
    public string Id { get; set; } = string.Empty;
    public string LocalId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Image { get; set; }
    public string? Category { get; set; } // "Pokemon", "Trainer", "Energy"
    public string? Illustrator { get; set; }
    public string? Rarity { get; set; }
    public TcgdexVariants? Variants { get; set; }
    [JsonPropertyName("variants_detailed")]
    public List<TcgdexVariantDetailed>? VariantsDetailed { get; set; }
    public TcgdexSetResume? Set { get; set; }
    public int? Hp { get; set; }
    public List<string>? Types { get; set; }
    public string? Stage { get; set; }
    public string? Suffix { get; set; }
    public string? EvolveFrom { get; set; }
    public List<int>? DexId { get; set; }
    [JsonPropertyName("cameoDexIds")]
    public List<int>? CameoDexIds { get; set; }
    public string? Description { get; set; }
    public List<TcgdexAttack>? Attacks { get; set; }
    public List<TcgdexAbility>? Abilities { get; set; }
    public TcgdexItem? Item { get; set; }
    public List<TcgdexWeakRes>? Weaknesses { get; set; }
    public List<TcgdexWeakRes>? Resistances { get; set; }
    public int? Retreat { get; set; }
    public string? RegulationMark { get; set; }
    public TcgdexLegalities? Legal { get; set; }
    public string? Updated { get; set; }
    public string? TrainerType { get; set; }
    public string? EnergyType { get; set; }
    public string? Effect { get; set; }
    public TcgdexThirdParty? ThirdParty { get; set; }
}

/// <summary>
/// Custom converter allowing numeric or string damage values (e.g. 90, "90", "20+", "10x").
/// </summary>
public sealed class FlexibleDamageConverter : JsonConverter<string>
{
    public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return reader.TokenType switch
        {
            JsonTokenType.Number => reader.TryGetInt64(out var l) ? l.ToString() : reader.GetDouble().ToString(),
            JsonTokenType.String => reader.GetString(),
            JsonTokenType.Null => null,
            _ => null
        };
    }

    public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value);
    }
}
