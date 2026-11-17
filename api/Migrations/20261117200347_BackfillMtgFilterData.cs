using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace api.Migrations;

/// <inheritdoc />
public partial class BackfillMtgFilterData : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Older imports retained these values in JSON before filter columns existed.
        // Check the full string before casting: SQLite otherwise turns "*" into zero.
        foreach (var (column, property) in new[]
                 { ("Power", "power"), ("Toughness", "toughness"), ("Loyalty", "loyalty") })
        {
            migrationBuilder.Sql($$"""
                WITH Stats AS (
                    SELECT c.Id,
                           trim(CAST(json_extract(
                               CASE WHEN json_valid(c.ExtraCardDataJson) THEN c.ExtraCardDataJson ELSE '{}' END,
                               '$.{{property}}') AS TEXT)) AS Value
                    FROM Cards c JOIN Games g ON g.Id = c.GameId
                    WHERE g.Name = 'Magic The Gathering' COLLATE NOCASE
                ), Parsed AS (
                    SELECT Id, Value,
                           CASE WHEN substr(Value, 1, 1) IN ('+', '-') THEN substr(Value, 2) ELSE Value END AS Digits
                    FROM Stats
                )
                UPDATE Cards SET "{{column}}" = (
                    SELECT CAST(Value AS INTEGER) FROM Parsed WHERE Parsed.Id = Cards.Id
                )
                WHERE "{{column}}" IS NULL AND Id IN (
                    SELECT Id FROM Parsed
                    WHERE Digits <> '' AND Digits NOT GLOB '*[^0-9]*'
                      AND CAST(Value AS INTEGER) BETWEEN -2147483648 AND 2147483647
                );
                """);
        }

        migrationBuilder.Sql("""
            WITH Colors(Name, Color) AS (VALUES
                ('White', '#F8F6D8'), ('Blue', '#0E68AB'), ('Black', '#150B00'),
                ('Red', '#D3202A'), ('Green', '#00733E'), ('Colorless', '#CAC5C0'))
            INSERT INTO Aspects (GameId, Name, Color)
            SELECT g.Id, colors.Name, colors.Color FROM Games g CROSS JOIN Colors colors
            WHERE g.Name = 'Magic The Gathering' COLLATE NOCASE
              AND NOT EXISTS (
                  SELECT 1 FROM Aspects a WHERE a.GameId = g.Id AND a.Name = colors.Name COLLATE NOCASE
              );

            WITH MtgCards AS (
                SELECT c.Id, c.GameId,
                       CASE WHEN json_type(c.ExtraCardDataJson, '$.colorIdentity') = 'array'
                            THEN json_extract(c.ExtraCardDataJson, '$.colorIdentity') ELSE '[]' END AS IdentityJson
                FROM Cards c JOIN Games g ON g.Id = c.GameId
                WHERE g.Name = 'Magic The Gathering' COLLATE NOCASE
                  AND json_valid(c.ExtraCardDataJson)
                  AND NOT EXISTS (SELECT 1 FROM CardColorIdentities ci WHERE ci.CardId = c.Id)
            ), Identities AS (
                SELECT c.Id AS CardId, c.GameId,
                       CASE upper(trim(j.value))
                           WHEN 'W' THEN 'White' WHEN 'U' THEN 'Blue' WHEN 'B' THEN 'Black'
                           WHEN 'R' THEN 'Red' WHEN 'G' THEN 'Green' WHEN 'C' THEN 'Colorless'
                       END AS Name
                FROM MtgCards c, json_each(c.IdentityJson) j
                UNION
                SELECT Id, GameId, 'Colorless' FROM MtgCards WHERE json_array_length(IdentityJson) = 0
            )
            INSERT INTO CardColorIdentities (CardId, AspectId)
            SELECT DISTINCT i.CardId, a.Id FROM Identities i
            JOIN Aspects a ON a.GameId = i.GameId AND a.Name = i.Name COLLATE NOCASE;
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Preserve recovered catalog data; rollback cannot distinguish it from later imports.
    }
}
