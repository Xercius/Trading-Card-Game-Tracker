using System.Text.Json;
using System.Text.Json.Serialization;

namespace api.Importing.Swccg;

/// <summary>
/// Deserializes a JSON array that may contain plain strings or objects with a 'title' / 'name' property (e.g. underlyingCardFor).
/// </summary>
public sealed class SwccgFlexibleStringListConverter : JsonConverter<List<string>>
{
    public override List<string> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
        {
            return [];
        }

        if (reader.TokenType == JsonTokenType.String)
        {
            var str = reader.GetString();
            return string.IsNullOrWhiteSpace(str) ? [] : [str];
        }

        if (reader.TokenType != JsonTokenType.StartArray)
        {
            reader.Skip();
            return [];
        }

        var list = new List<string>();
        while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
        {
            if (reader.TokenType == JsonTokenType.String)
            {
                var str = reader.GetString();
                if (!string.IsNullOrWhiteSpace(str)) list.Add(str);
            }
            else if (reader.TokenType == JsonTokenType.StartObject)
            {
                using var doc = JsonDocument.ParseValue(ref reader);
                if (doc.RootElement.TryGetProperty("title", out var titleProp) && titleProp.ValueKind == JsonValueKind.String)
                {
                    var title = titleProp.GetString();
                    if (!string.IsNullOrWhiteSpace(title)) list.Add(title);
                }
                else if (doc.RootElement.TryGetProperty("name", out var nameProp) && nameProp.ValueKind == JsonValueKind.String)
                {
                    var name = nameProp.GetString();
                    if (!string.IsNullOrWhiteSpace(name)) list.Add(name);
                }
            }
            else
            {
                reader.Skip();
            }
        }

        return list;
    }

    public override void Write(Utf8JsonWriter writer, List<string> value, JsonSerializerOptions options)
    {
        JsonSerializer.Serialize(writer, value, options);
    }
}

/// <summary>
/// Represents an expansion set entry from swccg-card-json sets.json.
/// </summary>
public sealed record SwccgSet(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("name")] string Name
);

/// <summary>
/// Represents a printing reference within an SWCCG card object.
/// </summary>
public sealed record SwccgPrintingRef(
    [property: JsonPropertyName("set")] string? Set,
    [property: JsonPropertyName("gempId")] string? GempId
);

/// <summary>
/// Represents an SWCCG card entry from swccg-card-json (Dark.json or Light.json).
/// </summary>
public sealed record SwccgCardRecord(
    [property: JsonPropertyName("id")] int Id,
    [property: JsonPropertyName("gempId")] string? GempId,
    [property: JsonPropertyName("side")] string? Side,
    [property: JsonPropertyName("rarity")] string? Rarity,
    [property: JsonPropertyName("set")] string? Set,
    [property: JsonPropertyName("printings")] List<SwccgPrintingRef>? Printings,
    [property: JsonPropertyName("abbr")] List<string>? Abbr,
    [property: JsonPropertyName("combo")] List<string>? Combo,
    [property: JsonPropertyName("matching")] List<string>? Matching,
    [property: JsonPropertyName("matchingWeapon")] List<string>? MatchingWeapon,
    [property: JsonPropertyName("pulls")] List<string>? Pulls,
    [property: JsonPropertyName("pulledBy")] List<string>? PulledBy,
    [property: JsonPropertyName("underlyingCardFor"), JsonConverter(typeof(SwccgFlexibleStringListConverter))] List<string>? UnderlyingCardFor,
    [property: JsonPropertyName("personas")] List<string>? Personas,
    [property: JsonPropertyName("rulings")] List<string>? Rulings,
    [property: JsonPropertyName("misc")] string? Misc,
    [property: JsonPropertyName("counterpart")] string? Counterpart,
    [property: JsonPropertyName("legacy")] bool? Legacy,
    [property: JsonPropertyName("front")] SwccgFaceRecord? Front,
    [property: JsonPropertyName("back")] SwccgFaceRecord? Back
);

/// <summary>
/// Represents the front or back face attributes of an SWCCG card.
/// </summary>
public sealed record SwccgFaceRecord(
    [property: JsonPropertyName("title")] string? Title,
    [property: JsonPropertyName("type")] string? Type,
    [property: JsonPropertyName("subType")] string? SubType,
    [property: JsonPropertyName("uniqueness")] string? Uniqueness,
    [property: JsonPropertyName("destiny")] string? Destiny,
    [property: JsonPropertyName("deploy")] string? Deploy,
    [property: JsonPropertyName("forfeit")] string? Forfeit,
    [property: JsonPropertyName("power")] string? Power,
    [property: JsonPropertyName("ability")] string? Ability,
    [property: JsonPropertyName("armor")] string? Armor,
    [property: JsonPropertyName("maneuver")] string? Maneuver,
    [property: JsonPropertyName("hyperspeed")] string? Hyperspeed,
    [property: JsonPropertyName("landspeed")] string? Landspeed,
    [property: JsonPropertyName("parsec")] string? Parsec,
    [property: JsonPropertyName("ferocity")] string? Ferocity,
    [property: JsonPropertyName("politics")] string? Politics,
    [property: JsonPropertyName("gametext")] string? Gametext,
    [property: JsonPropertyName("lore")] string? Lore,
    [property: JsonPropertyName("imageUrl")] string? ImageUrl,
    [property: JsonPropertyName("icons")] List<string>? Icons,
    [property: JsonPropertyName("characteristics")] List<string>? Characteristics,
    [property: JsonPropertyName("extraText")] List<string>? ExtraText
);

