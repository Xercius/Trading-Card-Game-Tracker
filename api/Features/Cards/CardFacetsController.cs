using api.Data;
using api.Features._Common;
using api.Features.Cards.Dtos;
using api.Features.Cards.Services;
using api.Models;
using api.Shared;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace api.Features.Cards;

/// <summary>
/// Provides API endpoints for retrieving filter facets (games, sets, rarities, types, arenas, aspects, traits) used in card search and browsing.
/// This controller supports the client-side filtering UI by returning distinct values from the card database
/// that can be used to populate dropdown filters and refine search results.
/// </summary>
[ApiController]
[Route("api/cards/facets")]
public sealed class CardFacetsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly PokemonFilterService _pokemonFilterService;
    private readonly PokemonEvolutionService _pokemonEvolutionService;

    /// <summary>
    /// Initializes a new instance of the <see cref="CardFacetsController"/> class.
    /// </summary>
    public CardFacetsController(
        AppDbContext db,
        PokemonFilterService pokemonFilterService,
        PokemonEvolutionService pokemonEvolutionService)
    {
        _db = db;
        _pokemonFilterService = pokemonFilterService;
        _pokemonEvolutionService = pokemonEvolutionService;
    }

    /// <summary>
    /// Retrieves facets (games, sets, rarities, types, arenas, aspects, traits) with counts based on active filters.
    /// Only returns facet values that produce results with the current filter combination.
    /// </summary>
    /// <param name="game">Optional comma-separated list of game names to filter by.</param>
    /// <param name="set">Optional comma-separated list of set names to filter by.</param>
    /// <param name="rarity">Optional comma-separated list of rarity values to filter by.</param>
    /// <param name="type">Optional comma-separated list of associated card types to filter by.</param>
    /// <param name="arena">Optional comma-separated list of associated card arenas to filter by.</param>
    /// <param name="aspect">Optional comma-separated list of associated card aspects to filter by.</param>
    /// <param name="aspectMethod">Filtering method for aspects ('or', 'and', 'exclude').</param>
    /// <param name="trait">Optional comma-separated list of associated card traits to filter by.</param>
    /// <param name="traitMethod">Filtering method for traits ('or', 'and', 'exclude').</param>
    /// <param name="keyword">Optional comma-separated list of associated card keywords to filter by.</param>
    /// <param name="keywordMethod">Filtering method for keywords ('or', 'and', 'exclude').</param>
    /// <param name="side">Optional card side/alignment to filter by (e.g. 'Dark', 'Light').</param>
    /// <param name="persona">Optional comma-separated list of character personas to filter by.</param>
    /// <param name="subType">Optional comma-separated list of sub-types to filter by.</param>
    /// <param name="destiny">Optional comma-separated list of destiny values to filter by.</param>
    /// <param name="deploy">Optional comma-separated list of deploy values to filter by.</param>
    /// <param name="forfeit">Optional comma-separated list of forfeit values to filter by.</param>
    /// <param name="power">Optional comma-separated list of power values to filter by.</param>
    /// <param name="ability">Optional comma-separated list of ability values to filter by.</param>
    /// <param name="lightSideIcon">Optional comma-separated list of light side icon counts to filter by.</param>
    /// <param name="darkSideIcon">Optional comma-separated list of dark side icon counts to filter by.</param>
    /// <param name="hp">Optional HP value or numeric comparison expression to filter by.</param>
    /// <param name="stage">Optional comma-separated list of Pokémon stages to filter by.</param>
    /// <param name="specialType">Optional comma-separated list of special Pokémon types to filter by.</param>
    /// <param name="evolvesFrom">Optional Pokémon name to filter by its full evolutionary lineage.</param>
    /// <param name="q">Optional search term to filter cards or printings by.</param>
    /// <param name="printingSearch">Whether to also match printing number and set alongside card name and type.</param>
    /// <param name="ct">Cancellation token to stop the operation if needed.</param>
    /// <returns>
    /// An HTTP 200 OK response with a <see cref="CardsFacetResponse"/> containing
    /// lists of games, sets, rarities, types, arenas, aspects, and traits with their respective counts.
    /// Each facet list reflects the current filter state and only includes values that yield results.
    /// </returns>
    [HttpGet]
    public async Task<ActionResult<CardsFacetResponse>> GetFacets(
        [FromQuery] string? game,
        [FromQuery] string? set,
        [FromQuery] string? rarity,
        [FromQuery] string? type,
        [FromQuery] string? arena = null,
        [FromQuery] string? aspect = null,
        [FromQuery] string? aspectMethod = null,
        [FromQuery] string? trait = null,
        [FromQuery] string? traitMethod = null,
        [FromQuery] string? keyword = null,
        [FromQuery] string? keywordMethod = null,
        [FromQuery] string? side = null,
        [FromQuery] string? persona = null,
        [FromQuery] string? subType = null,
        [FromQuery] string? destiny = null,
        [FromQuery] string? deploy = null,
        [FromQuery] string? forfeit = null,
        [FromQuery] string? power = null,
        [FromQuery] string? ability = null,
        [FromQuery] string? lightSideIcon = null,
        [FromQuery] string? darkSideIcon = null,
        [FromQuery] string? hp = null,
        [FromQuery] string? stage = null,
        [FromQuery] string? specialType = null,
        [FromQuery] string? evolvesFrom = null,
        [FromQuery] string? q = null,
        [FromQuery] bool printingSearch = false,
        CancellationToken ct = default)
    {
        var games = CsvUtils.Parse(game);
        var sets = CsvUtils.Parse(set);
        var rarities = CsvUtils.Parse(rarity);
        var types = CsvUtils.Parse(type);
        var arenas = CsvUtils.Parse(arena);
        var aspects = CsvUtils.ParsePreservingDuplicates(aspect)
            .Where(a => !string.Equals(a, "All", StringComparison.OrdinalIgnoreCase))
            .ToList();
        var traits = CsvUtils.Parse(trait);
        var keywords = CsvUtils.Parse(keyword);
        var personas = CsvUtils.Parse(persona);
        var subTypes = CsvUtils.Parse(subType);
        var destinies = CsvUtils.Parse(destiny);
        var deploys = CsvUtils.Parse(deploy);
        var forfeits = CsvUtils.Parse(forfeit);
        var powers = CsvUtils.Parse(power);
        var abilities = CsvUtils.Parse(ability);
        var lightSideIcons = CsvUtils.Parse(lightSideIcon);
        var darkSideIcons = CsvUtils.Parse(darkSideIcon);
        var hps = CsvUtils.Parse(hp);
        var stages = CsvUtils.Parse(stage);
        var specialTypes = CsvUtils.Parse(specialType);
        var evolvesFromCardIds = !string.IsNullOrWhiteSpace(evolvesFrom)
            ? await _pokemonEvolutionService.ResolveLineageCardIdsAsync(evolvesFrom, ct)
            : null;
        var gamesNormalized = games.Select(SqliteCaseNormalizer.Normalize).ToList();
        var gameLookupQuery = _db.Games.AsNoTracking().AsQueryable();
        if (games.Count > 0)
            gameLookupQuery = gameLookupQuery.Where(candidate => gamesNormalized.Contains(candidate.NameNormalized));
        var gameIds = await gameLookupQuery
            .Select(candidate => candidate.Id)
            .ToListAsync(ct);

        // Build base query with filters
        var baseQuery = _db.CardPrintings.AsNoTracking().AsQueryable();

        // Apply search term filter (case-insensitive)
        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim();
            var pattern = $"%{term}%";
            if (printingSearch)
            {
                var normalizedTerm = SqliteCaseNormalizer.Normalize(term);
                var normalizedPattern = $"%{normalizedTerm}%";
                baseQuery = baseQuery.Where(cp =>
                    EF.Functions.Like(EF.Functions.Collate(cp.Card.Name, "NOCASE"), pattern) ||
                    cp.Card.CardTypes.Any(cardType => EF.Functions.Like(
                        EF.Functions.Collate(cardType.CardType.Name, "NOCASE"), pattern)) ||
                    (cp.Number != null && EF.Functions.Like(EF.Functions.Collate(cp.Number, "NOCASE"), pattern)) ||
                    (cp.CatalogSet != null && cp.CatalogSet.Name != null &&
                        EF.Functions.Like(EF.Functions.Collate(cp.CatalogSet.Name, "NOCASE"), pattern)) ||
                    (cp.CatalogSet != null && cp.CatalogSet.Code != null &&
                        EF.Functions.Like(EF.Functions.Collate(cp.CatalogSet.Code, "NOCASE"), pattern)));
            }
            else
            {
                baseQuery = baseQuery.Where(cp =>
                    EF.Functions.Like(EF.Functions.Collate(cp.Card.Name, "NOCASE"), pattern) ||
                    cp.Card.CardTypes.Any(cardType => EF.Functions.Like(
                        EF.Functions.Collate(cardType.CardType.Name, "NOCASE"), pattern)));
            }
        }

        var powerValues = ParseIntegers(powers);
        var abilityValues = ParseIntegers(abilities);
        var lightSideIconValues = ParseIntegers(lightSideIcons);
        var darkSideIconValues = ParseIntegers(darkSideIcons);

        IQueryable<CardPrinting> ApplyStatFilters(IQueryable<CardPrinting> query, string? excludedFilter)
        {
            if (excludedFilter != "subType" && subTypes.Count > 0)
            {
                var normalizedSubTypes = subTypes.Select(SqliteCaseNormalizer.Normalize).ToList();
                query = query.Where(p => p.Card.CardSubTypes.Any(st =>
                    normalizedSubTypes.Contains(EF.Functions.Collate(st.SubType.Name, "NOCASE"))));
            }
            if (excludedFilter != "destiny" && destinies.Count > 0)
            {
                var normalizedDestinies = destinies.Select(SqliteCaseNormalizer.Normalize).ToList();
                query = query.Where(p => p.Card.Destiny != null &&
                    normalizedDestinies.Contains(EF.Functions.Collate(p.Card.Destiny, "NOCASE")));
            }
            if (excludedFilter != "deploy" && deploys.Count > 0)
            {
                var normalizedDeploys = deploys.Select(SqliteCaseNormalizer.Normalize).ToList();
                query = query.Where(p => p.Card.Deploy != null &&
                    normalizedDeploys.Contains(EF.Functions.Collate(p.Card.Deploy, "NOCASE")));
            }
            if (excludedFilter != "forfeit" && forfeits.Count > 0)
            {
                var normalizedForfeits = forfeits.Select(SqliteCaseNormalizer.Normalize).ToList();
                query = query.Where(p => p.Card.Forfeit != null &&
                    normalizedForfeits.Contains(EF.Functions.Collate(p.Card.Forfeit, "NOCASE")));
            }
            if (excludedFilter != "power" && powers.Count > 0)
                query = query.ApplyNumericFilter(powers, p => p.Card.Power);
            if (excludedFilter != "ability" && abilities.Count > 0)
                query = query.ApplyNumericFilter(abilities, p => p.Card.Ability);
            if (excludedFilter != "lightSideIcon" && lightSideIcons.Count > 0)
                query = query.ApplyNumericFilter(lightSideIcons, p => p.Card.LightSideIcons);
            if (excludedFilter != "darkSideIcon" && darkSideIcons.Count > 0)
                query = query.ApplyNumericFilter(darkSideIcons, p => p.Card.DarkSideIcons);
            if (excludedFilter != "hp" && hps.Count > 0)
                query = query.ApplyNumericFilter(hps, p => p.Card.Hp);
            if (excludedFilter != "stage" && stages.Count > 0)
            {
                var normalizedStages = stages.Select(SqliteCaseNormalizer.Normalize).ToList();
                query = query.Where(p => p.Card.Stage != null &&
                    normalizedStages.Contains(EF.Functions.Collate(p.Card.Stage, "NOCASE")));
            }
            if (excludedFilter != "specialType" && specialTypes.Count > 0)
                query = _pokemonFilterService.ApplySpecialTypeFilter(query, specialTypes);
            if (excludedFilter != "evolvesFrom" && evolvesFromCardIds is not null)
                query = query.Where(p => evolvesFromCardIds.Contains(p.CardId));

            return query;
        }

        var queryWithoutStatFilters = baseQuery;
        baseQuery = ApplyStatFilters(baseQuery, null);

        IQueryable<CardPrinting> BuildStatFacetQuery(string excludedFilter)
        {
            var query = ApplyGameFilter(queryWithoutStatFilters, gameIds);
            query = ApplySetFilter(query, sets);
            query = ApplyRarityFilter(query, rarities);
            query = ApplyTypeFilter(query, types);
            query = ApplyArenaFilter(query, arenas);
            query = ApplyAspectFilter(query, aspects, aspectMethod);
            query = ApplyTraitFilter(query, traits, traitMethod);
            query = ApplyKeywordFilter(query, keywords, keywordMethod);
            query = ApplySideFilter(query, side);
            query = ApplyPersonaFilter(query, personas);
            return ApplyStatFilters(query, excludedFilter);
        }

        // Compute game facets
        var gameQuery = ApplySetFilter(baseQuery, sets);
        gameQuery = ApplyRarityFilter(gameQuery, rarities);
        gameQuery = ApplyTypeFilter(gameQuery, types);
        gameQuery = ApplyArenaFilter(gameQuery, arenas);
        gameQuery = ApplyAspectFilter(gameQuery, aspects, aspectMethod);
        gameQuery = ApplyTraitFilter(gameQuery, traits, traitMethod);
        gameQuery = ApplyKeywordFilter(gameQuery, keywords, keywordMethod);
        gameQuery = ApplySideFilter(gameQuery, side);
        gameQuery = ApplyPersonaFilter(gameQuery, personas);

        var gameFacets = await gameQuery
            .GroupBy(cp => cp.Card.Game!.Name)
            .Select(g => new FacetOption { Value = g.Key, Count = g.Count() })
            .OrderBy(f => f.Value)
            .ToListAsync(ct);

        // Compute set facets
        var setQuery = ApplyGameFilter(baseQuery, gameIds);
        setQuery = ApplyRarityFilter(setQuery, rarities);
        setQuery = ApplyTypeFilter(setQuery, types);
        setQuery = ApplyArenaFilter(setQuery, arenas);
        setQuery = ApplyAspectFilter(setQuery, aspects, aspectMethod);
        setQuery = ApplyTraitFilter(setQuery, traits, traitMethod);
        setQuery = ApplyKeywordFilter(setQuery, keywords, keywordMethod);
        setQuery = ApplySideFilter(setQuery, side);
        setQuery = ApplyPersonaFilter(setQuery, personas);

        var setFacets = await setQuery
            .Where(cp => cp.CatalogSet != null && (!string.IsNullOrWhiteSpace(cp.CatalogSet.Name) || !string.IsNullOrWhiteSpace(cp.CatalogSet.Code)))
            .GroupBy(cp => new
            {
                Value = (!string.IsNullOrWhiteSpace(cp.CatalogSet!.Name) ? cp.CatalogSet.Name : cp.CatalogSet.Code)!,
                Label = (!string.IsNullOrWhiteSpace(cp.CatalogSet.Name) ? cp.CatalogSet.Name : cp.CatalogSet.Code)!
            })
            .Select(g => new FacetOption { Value = g.Key.Value, Label = g.Key.Label, Count = g.Count() })
            .OrderBy(f => f.Label)
            .ToListAsync(ct);

        // Compute rarity facets
        var rarityQuery = ApplyGameFilter(baseQuery, gameIds);
        rarityQuery = ApplySetFilter(rarityQuery, sets);
        rarityQuery = ApplyTypeFilter(rarityQuery, types);
        rarityQuery = ApplyArenaFilter(rarityQuery, arenas);
        rarityQuery = ApplyAspectFilter(rarityQuery, aspects, aspectMethod);
        rarityQuery = ApplyTraitFilter(rarityQuery, traits, traitMethod);
        rarityQuery = ApplyKeywordFilter(rarityQuery, keywords, keywordMethod);
        rarityQuery = ApplySideFilter(rarityQuery, side);
        rarityQuery = ApplyPersonaFilter(rarityQuery, personas);

        var isSwccgGame = games.Count == 1 && games[0].Equals("Star Wars CCG", StringComparison.OrdinalIgnoreCase);

        var rarityFacetsQuery = isSwccgGame
            ? rarityQuery
                .Where(cp => cp.RarityLookup != null && !string.IsNullOrWhiteSpace(cp.RarityLookup.RarityType ?? cp.RarityLookup.Name))
                .GroupBy(cp => cp.RarityLookup!.RarityType ?? cp.RarityLookup.Name)
            : rarityQuery
                .Where(cp => cp.RarityLookup != null && !string.IsNullOrWhiteSpace(cp.RarityLookup.Name))
                .GroupBy(cp => cp.RarityLookup!.Name);

        var rarityFacets = await rarityFacetsQuery
            .Select(g => new FacetOption { Value = g.Key, Count = g.Count() })
            .OrderBy(f => f.Value)
            .ToListAsync(ct);

        // Compute type facets
        var typeQuery = ApplyGameFilter(baseQuery, gameIds);
        typeQuery = ApplySetFilter(typeQuery, sets);
        typeQuery = ApplyRarityFilter(typeQuery, rarities);
        typeQuery = ApplyArenaFilter(typeQuery, arenas);
        typeQuery = ApplyAspectFilter(typeQuery, aspects, aspectMethod);
        typeQuery = ApplyTraitFilter(typeQuery, traits, traitMethod);
        typeQuery = ApplyKeywordFilter(typeQuery, keywords, keywordMethod);
        typeQuery = ApplySideFilter(typeQuery, side);
        typeQuery = ApplyPersonaFilter(typeQuery, personas);

        var typePrintingCounts = await GetPrintingCountsByCardAsync(typeQuery, ct);
        var typeCardIds = typePrintingCounts.Keys.ToList();
        var typeAssociations = await _db.CardCardTypes
            .AsNoTracking()
            .Where(association => typeCardIds.Contains(association.CardId))
            .Select(association => new { association.CardId, association.CardType.Name })
            .ToListAsync(ct);
        var typeFacets = BuildFacets(
            typeAssociations.Select(a => (a.CardId, a.Name)),
            typePrintingCounts);

        // Compute arena facets
        var arenaQuery = ApplyGameFilter(baseQuery, gameIds);
        arenaQuery = ApplySetFilter(arenaQuery, sets);
        arenaQuery = ApplyRarityFilter(arenaQuery, rarities);
        arenaQuery = ApplyTypeFilter(arenaQuery, types);
        arenaQuery = ApplyAspectFilter(arenaQuery, aspects, aspectMethod);
        arenaQuery = ApplyTraitFilter(arenaQuery, traits, traitMethod);
        arenaQuery = ApplyKeywordFilter(arenaQuery, keywords, keywordMethod);
        arenaQuery = ApplySideFilter(arenaQuery, side);
        arenaQuery = ApplyPersonaFilter(arenaQuery, personas);

        var arenaPrintingCounts = await GetPrintingCountsByCardAsync(arenaQuery, ct);
        var arenaCardIds = arenaPrintingCounts.Keys.ToList();
        var arenaAssociations = await _db.CardArenas
            .AsNoTracking()
            .Where(association => arenaCardIds.Contains(association.CardId))
            .Select(association => new { association.CardId, association.Arena.Name })
            .ToListAsync(ct);
        var arenaFacets = BuildFacets(
            arenaAssociations.Select(a => (a.CardId, a.Name)),
            arenaPrintingCounts);

        // Compute aspect facets
        var aspectQuery = ApplyGameFilter(baseQuery, gameIds);
        aspectQuery = ApplySetFilter(aspectQuery, sets);
        aspectQuery = ApplyRarityFilter(aspectQuery, rarities);
        aspectQuery = ApplyTypeFilter(aspectQuery, types);
        aspectQuery = ApplyArenaFilter(aspectQuery, arenas);
        aspectQuery = ApplyTraitFilter(aspectQuery, traits, traitMethod);
        aspectQuery = ApplyKeywordFilter(aspectQuery, keywords, keywordMethod);
        aspectQuery = ApplySideFilter(aspectQuery, side);
        aspectQuery = ApplyPersonaFilter(aspectQuery, personas);

        var aspectPrintingCounts = await GetPrintingCountsByCardAsync(aspectQuery, ct);
        var aspectCardIds = aspectPrintingCounts.Keys.ToList();
        var aspectAssociations = await _db.CardAspects
            .AsNoTracking()
            .Where(association => aspectCardIds.Contains(association.CardId))
            .Select(association => new { association.CardId, association.Aspect.Name })
            .ToListAsync(ct);
        var aspectFacets = BuildFacets(aspectAssociations.Select(a => (a.CardId, a.Name)), aspectPrintingCounts);

        // Compute trait facets
        var traitQuery = ApplyGameFilter(baseQuery, gameIds);
        traitQuery = ApplySetFilter(traitQuery, sets);
        traitQuery = ApplyRarityFilter(traitQuery, rarities);
        traitQuery = ApplyTypeFilter(traitQuery, types);
        traitQuery = ApplyArenaFilter(traitQuery, arenas);
        traitQuery = ApplyAspectFilter(traitQuery, aspects, aspectMethod);
        traitQuery = ApplyKeywordFilter(traitQuery, keywords, keywordMethod);
        traitQuery = ApplySideFilter(traitQuery, side);
        traitQuery = ApplyPersonaFilter(traitQuery, personas);

        var traitPrintingCounts = await GetPrintingCountsByCardAsync(traitQuery, ct);
        var traitCardIds = traitPrintingCounts.Keys.ToList();
        var traitAssociations = await _db.CardTraits
            .AsNoTracking()
            .Where(association => traitCardIds.Contains(association.CardId))
            .Select(association => new { association.CardId, association.Trait.Name })
            .ToListAsync(ct);
        var traitFacets = BuildFacets(traitAssociations.Select(a => (a.CardId, a.Name)), traitPrintingCounts);

        // Compute keyword facets
        var keywordQuery = ApplyGameFilter(baseQuery, gameIds);
        keywordQuery = ApplySetFilter(keywordQuery, sets);
        keywordQuery = ApplyRarityFilter(keywordQuery, rarities);
        keywordQuery = ApplyTypeFilter(keywordQuery, types);
        keywordQuery = ApplyArenaFilter(keywordQuery, arenas);
        keywordQuery = ApplyAspectFilter(keywordQuery, aspects, aspectMethod);
        keywordQuery = ApplyTraitFilter(keywordQuery, traits, traitMethod);
        keywordQuery = ApplySideFilter(keywordQuery, side);
        keywordQuery = ApplyPersonaFilter(keywordQuery, personas);

        var keywordPrintingCounts = await GetPrintingCountsByCardAsync(keywordQuery, ct);
        var keywordCardIds = keywordPrintingCounts.Keys.ToList();
        var keywordAssociations = await _db.CardKeywords
            .AsNoTracking()
            .Where(association => keywordCardIds.Contains(association.CardId))
            .Select(association => new { association.CardId, association.Keyword.Name })
            .ToListAsync(ct);
        var keywordFacets = BuildFacets(keywordAssociations.Select(a => (a.CardId, a.Name)), keywordPrintingCounts);

        var treatmentQuery = ApplyGameFilter(baseQuery, gameIds);
        treatmentQuery = ApplySetFilter(treatmentQuery, sets);
        treatmentQuery = ApplyRarityFilter(treatmentQuery, rarities);
        treatmentQuery = ApplyTypeFilter(treatmentQuery, types);
        treatmentQuery = ApplyArenaFilter(treatmentQuery, arenas);
        treatmentQuery = ApplyAspectFilter(treatmentQuery, aspects, aspectMethod);
        treatmentQuery = ApplyTraitFilter(treatmentQuery, traits, traitMethod);
        treatmentQuery = ApplyKeywordFilter(treatmentQuery, keywords, keywordMethod);
        treatmentQuery = ApplySideFilter(treatmentQuery, side);
        treatmentQuery = ApplyPersonaFilter(treatmentQuery, personas);
        var treatmentFacets = await treatmentQuery
            .Where(printing => printing.Treatment != null)
            .GroupBy(printing => printing.Treatment!.Name)
            .Select(group => new FacetOption { Value = group.Key, Count = group.Count() })
            .OrderBy(facet => facet.Value)
            .ToListAsync(ct);

        // Compute persona facets
        var personaQuery = ApplyGameFilter(baseQuery, gameIds);
        personaQuery = ApplySideFilter(personaQuery, side);
        personaQuery = ApplySetFilter(personaQuery, sets);
        personaQuery = ApplyRarityFilter(personaQuery, rarities);
        personaQuery = ApplyTypeFilter(personaQuery, types);
        personaQuery = ApplyArenaFilter(personaQuery, arenas);
        personaQuery = ApplyAspectFilter(personaQuery, aspects, aspectMethod);
        personaQuery = ApplyTraitFilter(personaQuery, traits, traitMethod);
        personaQuery = ApplyKeywordFilter(personaQuery, keywords, keywordMethod);

        var rawPersonas = await personaQuery
            .Where(p => p.Card.Persona != null && p.Card.Persona != "")
            .Select(p => p.Card.Persona!)
            .ToListAsync(ct);

        var personaCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in rawPersonas)
        {
            var parts = p.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            foreach (var part in parts.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                personaCounts[part] = personaCounts.GetValueOrDefault(part, 0) + 1;
            }
        }

        var personaFacets = personaCounts
            .Select(kvp => new FacetOption { Value = kvp.Key, Count = kvp.Value })
            .OrderBy(f => f.Value)
            .ToList();

        // Compute subtype facets
        var subTypeQuery = BuildStatFacetQuery("subType");
        var subTypePrintingCounts = await GetPrintingCountsByCardAsync(subTypeQuery, ct);
        var subTypeCardIds = subTypePrintingCounts.Keys.ToList();
        var subTypeAssociations = await _db.CardSubTypes
            .AsNoTracking()
            .Where(association => subTypeCardIds.Contains(association.CardId))
            .Select(association => new { association.CardId, association.SubType.Name })
            .ToListAsync(ct);
        var subTypeFacets = BuildFacets(
            subTypeAssociations.Select(a => (a.CardId, a.Name)),
            subTypePrintingCounts);

        var destinyFacets = (await BuildStatFacetQuery("destiny")
            .Where(cp => cp.Card.Destiny != null && cp.Card.Destiny != "")
            .GroupBy(cp => cp.Card.Destiny!)
            .Select(g => new FacetOption { Value = g.Key, Count = g.Count() })
            .ToListAsync(ct))
            .OrderBy(f => int.TryParse(f.Value, out var n) ? n : int.MaxValue)
            .ThenBy(f => f.Value)
            .ToList();

        var deployFacets = (await BuildStatFacetQuery("deploy")
            .Where(cp => cp.Card.Deploy != null && cp.Card.Deploy != "")
            .GroupBy(cp => cp.Card.Deploy!)
            .Select(g => new FacetOption { Value = g.Key, Count = g.Count() })
            .ToListAsync(ct))
            .OrderBy(f => int.TryParse(f.Value, out var n) ? n : int.MaxValue)
            .ThenBy(f => f.Value)
            .ToList();

        var forfeitFacets = (await BuildStatFacetQuery("forfeit")
            .Where(cp => cp.Card.Forfeit != null && cp.Card.Forfeit != "")
            .GroupBy(cp => cp.Card.Forfeit!)
            .Select(g => new FacetOption { Value = g.Key, Count = g.Count() })
            .ToListAsync(ct))
            .OrderBy(f => int.TryParse(f.Value, out var n) ? n : int.MaxValue)
            .ThenBy(f => f.Value)
            .ToList();

        var rawPowers = await BuildStatFacetQuery("power")
            .Where(cp => cp.Card.Power != null)
            .GroupBy(cp => cp.Card.Power!.Value)
            .Select(g => new { Value = g.Key, Count = g.Count() })
            .ToListAsync(ct);
        var powerFacets = rawPowers
            .OrderBy(p => p.Value)
            .Select(p => new FacetOption { Value = p.Value.ToString(), Count = p.Count })
            .ToList();

        var rawAbilities = await BuildStatFacetQuery("ability")
            .Where(cp => cp.Card.Ability != null)
            .GroupBy(cp => cp.Card.Ability!.Value)
            .Select(g => new { Value = g.Key, Count = g.Count() })
            .ToListAsync(ct);
        var abilityFacets = rawAbilities
            .OrderBy(a => a.Value)
            .Select(a => new FacetOption { Value = a.Value.ToString(), Count = a.Count })
            .ToList();

        var rawLsIcons = await BuildStatFacetQuery("lightSideIcon")
            .Where(cp => cp.Card.LightSideIcons != null)
            .GroupBy(cp => cp.Card.LightSideIcons!.Value)
            .Select(g => new { Value = g.Key, Count = g.Count() })
            .ToListAsync(ct);
        var lightSideIconFacets = rawLsIcons
            .OrderBy(i => i.Value)
            .Select(i => new FacetOption { Value = i.Value.ToString(), Count = i.Count })
            .ToList();

        var rawDsIcons = await BuildStatFacetQuery("darkSideIcon")
            .Where(cp => cp.Card.DarkSideIcons != null)
            .GroupBy(cp => cp.Card.DarkSideIcons!.Value)
            .Select(g => new { Value = g.Key, Count = g.Count() })
            .ToListAsync(ct);
        var darkSideIconFacets = rawDsIcons
            .OrderBy(i => i.Value)
            .Select(i => new FacetOption { Value = i.Value.ToString(), Count = i.Count })
            .ToList();

        var rawStages = await BuildStatFacetQuery("stage")
            .Where(cp => cp.Card.Stage != null && cp.Card.Stage != "")
            .GroupBy(cp => cp.Card.Stage!)
            .Select(g => new { Value = g.Key, Count = g.Count() })
            .ToListAsync(ct);
        var stageFacets = rawStages
            .OrderBy(s => s.Value)
            .Select(s => new FacetOption { Value = s.Value, Label = s.Value, Count = s.Count })
            .ToList();

        var isPokemonScope = games.Count == 0
            || gamesNormalized.Contains("pokemon tcg")
            || gamesNormalized.Contains("pokemontcg");
        var specialTypeFacets = isPokemonScope
            ? await _pokemonFilterService.ComputeSpecialTypeFacetsAsync(BuildStatFacetQuery("specialType"), ct)
            : Array.Empty<FacetOption>();

        var response = new CardsFacetResponse
        {
            Games = gameFacets,
            Sets = setFacets,
            Rarities = rarityFacets,
            Types = typeFacets,
            Arenas = arenaFacets,
            Aspects = aspectFacets,
            Traits = traitFacets,
            Keywords = keywordFacets,
            Treatments = treatmentFacets,
            Personas = personaFacets,
            SubTypes = subTypeFacets,
            Destinies = destinyFacets,
            Deploys = deployFacets,
            Forfeits = forfeitFacets,
            Powers = powerFacets,
            Abilities = abilityFacets,
            LightSideIcons = lightSideIconFacets,
            DarkSideIcons = darkSideIconFacets,
            Stages = stageFacets,
            SpecialTypes = specialTypeFacets,
        };

        return Ok(response);
    }

    /// <summary>
    /// Retrieves a distinct, sorted list of all games available in the card database.
    /// </summary>
    /// <param name="ct">Cancellation token to stop the operation if needed.</param>
    /// <returns>
    /// An HTTP 200 OK response with a list of game names in alphabetical order.
    /// The list includes all unique games from card printings in the database.
    /// </returns>
    [HttpGet("games")]
    public async Task<ActionResult<IReadOnlyList<string>>> GetGames(CancellationToken ct = default)
    {
        var games = await _db.CardPrintings
            .AsNoTracking()
            .Select(cp => cp.Card.Game!.Name)
            .Distinct()
            .OrderBy(game => game)
            .ToListAsync(ct);

        return Ok(games);
    }

    /// <summary>
    /// Retrieves a distinct, sorted list of card sets, optionally filtered by game(s).
    /// </summary>
    /// <param name="game">
    /// Optional comma-separated list of game names to filter by. If provided, only sets from those games are returned.
    /// If empty or null, returns sets from all games.
    /// </param>
    /// <param name="ct">Cancellation token to stop the operation if needed.</param>
    /// <returns>
    /// An HTTP 200 OK response with a <see cref="CardFacetSetsResponse"/> containing:
    /// - A list of set codes in alphabetical order
    /// - The game name (if exactly one game was specified in the filter)
    /// Excludes any sets with null or whitespace names.
    /// </returns>
    [HttpGet("sets")]
    public async Task<ActionResult<CardFacetSetsResponse>> GetSets([FromQuery] string? game, CancellationToken ct = default)
    {
        var games = CsvUtils.Parse(game);
        var normalizedGames = games.Select(SqliteCaseNormalizer.Normalize).ToList();
        var gameLookupQuery = _db.Games.AsNoTracking().AsQueryable();
        if (games.Count > 0)
            gameLookupQuery = gameLookupQuery.Where(candidate => normalizedGames.Contains(candidate.NameNormalized));
        var gameIds = await gameLookupQuery
            .Select(candidate => candidate.Id)
            .ToListAsync(ct);
        var query = ApplyGameFilter(_db.CardPrintings.AsNoTracking(), gameIds);

        var sets = await query
            .Select(cp => cp.CatalogSet == null ? null : (!string.IsNullOrWhiteSpace(cp.CatalogSet.Name) ? cp.CatalogSet.Name : cp.CatalogSet.Code))
            .Where(s => s != null && !string.IsNullOrWhiteSpace(s))
            .Select(s => s!)
            .Distinct()
            .OrderBy(set => set)
            .ToListAsync(ct);

        var response = new CardFacetSetsResponse
        {
            Game = games.Count == 1 ? games[0] : null,
            Sets = sets,
        };

        return Ok(response);
    }

    /// <summary>
    /// Retrieves a distinct, sorted list of card rarities, optionally filtered by game(s).
    /// </summary>
    /// <param name="game">
    /// Optional comma-separated list of game names to filter by. If provided, only rarities from those games are returned.
    /// If empty or null, returns rarities from all games.
    /// </param>
    /// <param name="ct">Cancellation token to stop the operation if needed.</param>
    /// <returns>
    /// An HTTP 200 OK response with a <see cref="CardFacetRaritiesResponse"/> containing:
    /// - A list of rarity values in alphabetical order
    /// - The game name (if exactly one game was specified in the filter)
    /// Excludes any rarities with null or whitespace values.
    /// </returns>
    [HttpGet("rarities")]
    public async Task<ActionResult<CardFacetRaritiesResponse>> GetRarities([FromQuery] string? game, CancellationToken ct = default)
    {
        var games = CsvUtils.Parse(game);
        var normalizedGames = games.Select(SqliteCaseNormalizer.Normalize).ToList();
        var gameLookupQuery = _db.Games.AsNoTracking().AsQueryable();
        if (games.Count > 0)
            gameLookupQuery = gameLookupQuery.Where(candidate => normalizedGames.Contains(candidate.NameNormalized));
        var gameIds = await gameLookupQuery
            .Select(candidate => candidate.Id)
            .ToListAsync(ct);
        var query = ApplyGameFilter(_db.CardPrintings.AsNoTracking(), gameIds);

        var isSwccg = games.Count == 1 && games[0].Equals("Star Wars CCG", StringComparison.OrdinalIgnoreCase);
        var raritiesQuery = isSwccg
            ? query.Select(cp => cp.RarityLookup == null ? null : (cp.RarityLookup.RarityType ?? cp.RarityLookup.Name))
            : query.Select(cp => cp.RarityLookup == null ? null : cp.RarityLookup.Name);

        var rarities = await raritiesQuery
            .Where(r => r != null && !string.IsNullOrWhiteSpace(r))
            .Select(r => r!)
            .Distinct()
            .OrderBy(rarity => rarity)
            .ToListAsync(ct);

        var response = new CardFacetRaritiesResponse
        {
            Game = games.Count == 1 ? games[0] : null,
            Rarities = rarities,
        };

        return Ok(response);
    }

    private static async Task<Dictionary<int, int>> GetPrintingCountsByCardAsync(
        IQueryable<CardPrinting> query,
        CancellationToken ct)
    {
        var printingCounts = await query
            .GroupBy(printing => printing.CardId)
            .Select(group => new { CardId = group.Key, Count = group.Count() })
            .ToListAsync(ct);
        return printingCounts.ToDictionary(item => item.CardId, item => item.Count);
    }

    private static List<FacetOption> BuildFacets(
        IEnumerable<(int CardId, string Name)> associations,
        IReadOnlyDictionary<int, int> printingCountsByCard)
        => associations
            .Where(item => !string.IsNullOrWhiteSpace(item.Name) && printingCountsByCard.ContainsKey(item.CardId))
            .GroupBy(item => (item.CardId, Name: item.Name.Trim()))
            .GroupBy(group => group.Key.Name)
            .Select(group => new FacetOption
            {
                Value = group.Key,
                Count = group.Sum(item => printingCountsByCard[item.Key.CardId])
            })
            .OrderBy(facet => facet.Value)
            .ToList();

    private static List<int> ParseIntegers(IEnumerable<string> values)
        => values.Select(value => int.TryParse(value, out var parsed) ? (int?)parsed : null)
            .Where(value => value.HasValue)
            .Select(value => value!.Value)
            .ToList();

    private static IQueryable<CardPrinting> ApplyGameFilter(
        IQueryable<CardPrinting> query,
        IReadOnlyCollection<int> gameIds)
    {
        return gameIds.Count == 0
            ? query.Where(_ => false)
            : query.Where(printing => gameIds.Contains(printing.Card.GameId));
    }

    private static IQueryable<CardPrinting> ApplySetFilter(
        IQueryable<CardPrinting> query,
        IReadOnlyCollection<string> sets)
    {
        if (sets.Count == 0) return query;
        var normalizedSets = sets.Select(SqliteCaseNormalizer.Normalize).ToList();
        return query.Where(printing => printing.CatalogSet != null
            && (normalizedSets.Contains(EF.Functions.Collate(printing.CatalogSet.Code, "NOCASE")) ||
                printing.CatalogSet.Name != null && normalizedSets.Contains(
                    EF.Functions.Collate(printing.CatalogSet.Name, "NOCASE"))));
    }

    private static IQueryable<CardPrinting> ApplyRarityFilter(
        IQueryable<CardPrinting> query,
        IReadOnlyCollection<string> rarities)
    {
        if (rarities.Count == 0) return query;
        var normalizedRarities = rarities.Select(SqliteCaseNormalizer.Normalize).ToList();
        return query.Where(printing => printing.RarityLookup != null
            && (
                (printing.RarityLookup.RarityType != null && normalizedRarities.Contains(EF.Functions.Collate(printing.RarityLookup.RarityType, "NOCASE")))
                || normalizedRarities.Contains(EF.Functions.Collate(printing.RarityLookup.Name, "NOCASE"))
            ));
    }

    private static IQueryable<CardPrinting> ApplyTypeFilter(
        IQueryable<CardPrinting> query,
        IReadOnlyCollection<string> types)
    {
        if (types.Count == 0) return query;
        var normalizedTypes = types.Select(SqliteCaseNormalizer.Normalize).ToList();
        return query.Where(printing => printing.Card.CardTypes.Any(cardType =>
                normalizedTypes.Contains(EF.Functions.Collate(cardType.CardType.Name, "NOCASE"))));
    }

    private static IQueryable<CardPrinting> ApplyArenaFilter(
        IQueryable<CardPrinting> query,
        IReadOnlyCollection<string> arenas)
    {
        if (arenas.Count == 0) return query;
        var normalizedArenas = arenas.Select(SqliteCaseNormalizer.Normalize).ToList();
        return query.Where(printing => printing.Card.CardArenas.Any(cardArena =>
                normalizedArenas.Contains(EF.Functions.Collate(cardArena.Arena.Name, "NOCASE"))));
    }

    private static IQueryable<CardPrinting> ApplyAspectFilter(
        IQueryable<CardPrinting> query,
        IReadOnlyCollection<string> aspects,
        string? aspectMethod = null)
        => query.ApplyAspectFilter(aspects, aspectMethod);

    private static IQueryable<CardPrinting> ApplyTraitFilter(
        IQueryable<CardPrinting> query,
        IReadOnlyCollection<string> traits,
        string? traitMethod = null)
        => query.ApplyTraitFilter(traits, traitMethod);

    private static IQueryable<CardPrinting> ApplyKeywordFilter(
        IQueryable<CardPrinting> query,
        IReadOnlyCollection<string> keywords,
        string? keywordMethod = null)
        => query.ApplyKeywordFilter(keywords, keywordMethod);

    private static IQueryable<CardPrinting> ApplySideFilter(
        IQueryable<CardPrinting> query,
        string? side)
    {
        if (string.IsNullOrWhiteSpace(side)) return query;
        var s = side.Trim();
        return query.Where(p => p.Card.Side != null && EF.Functions.Collate(p.Card.Side, "NOCASE") == s);
    }

    private static IQueryable<CardPrinting> ApplyPersonaFilter(
        IQueryable<CardPrinting> query,
        IReadOnlyCollection<string> personas)
    {
        if (personas.Count == 0) return query;
        return query.Where(p => p.Card.Persona != null &&
            personas.Select(p => $"%,{p.Trim()},%").Any(pattern =>
                EF.Functions.Like("," + p.Card.Persona.Replace(", ", ",") + ",", pattern)));
    }
}
