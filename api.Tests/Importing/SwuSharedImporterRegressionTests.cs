using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Web;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using api.Data;
using api.Importing;
using api.Models;
using api.Tests.Features.AdminImport;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace api.Tests.Importing;

public sealed class SwuSharedImporterRegressionTests
{
    [Fact]
    public async Task Import_MultiplePrintingsWithSameTitle_SharesCardAndPreservesOwnedPrintingIds()
    {
        await using var database = await Database.CreateAsync();
        int firstId;
        int secondId;
        await using (var seed = database.Open())
        {
            var game = await seed.Games.SingleAsync(candidate => candidate.Name == "Star Wars Unlimited");
            var card = new Card { Game = game, Name = "Same title" };
            var first = new CardPrinting
            {
                Card = card,
                CatalogSet = TreatmentLookup.UpsertSet(seed, card.Game, "SOR"),
                Number = "one",
                RarityLookup = TreatmentLookup.UpsertRarity(seed, card.Game, "Common"),
                Treatment = TreatmentLookup.Upsert(seed, card.Game, "Standard"),
                DetailsJson = """{"sourceId":1,"cardUid":"legacy-1","serialCode":"one"}"""
            };
            var second = new CardPrinting
            {
                Card = card,
                CatalogSet = TreatmentLookup.UpsertSet(seed, card.Game, "SOR"),
                Number = "two",
                RarityLookup = TreatmentLookup.UpsertRarity(seed, card.Game, "Common"),
                Treatment = TreatmentLookup.Upsert(seed, card.Game, "Standard"),
                DetailsJson = """{"sourceId":2,"cardUid":"legacy-2","serialCode":"two"}"""
            };
            seed.CardPrintings.AddRange(first, second);
            var user = new User { Username = "owner", DisplayName = "Owner" };
            seed.UserCards.Add(new UserCard { User = user, CardPrinting = second, QuantityOwned = 3 });
            seed.DeckCards.Add(new DeckCard
            {
                Deck = new Deck { User = user, Name = "Deck", Game = "Star Wars Unlimited" },
                CardPrinting = first,
                QuantityInDeck = 2
            });
            await seed.SaveChangesAsync();
            firstId = first.Id;
            secondId = second.Id;
        }
        var records = new[]
        {
            Record(1, "Same title", "SOR", "one", uid: "legacy-1"),
            Record(2, "Same title", "SOR", "two", uid: "legacy-2")
        };
        Assert.Equal(0, (await ImportAsync(database, records)).Errors);
        var replay = await ImportAsync(database, records);
        Assert.Equal(0, replay.Errors);
        Assert.Equal(0, replay.CardsCreated + replay.CardsUpdated + replay.PrintingsCreated + replay.PrintingsUpdated);
        await using var db = database.Open();
        Assert.Equal(1, await db.Cards.CountAsync());
        var printings = (await db.CardPrintings.Include(p => p.Card).ToListAsync())
            .OrderBy(SourceId).ToArray();
        Assert.Equal(new[] { firstId, secondId }, printings.Select(p => p.Id));
        Assert.Equal(printings[0].CardId, printings[1].CardId);
        Assert.Equal("legacy-1", printings[0].CardUid);
        Assert.Equal("legacy-2", printings[1].CardUid);
        Assert.Equal("legacy-1", printings[0].Card.CardUid);
        Assert.Equal(secondId, (await db.UserCards.SingleAsync()).CardPrintingId);
        Assert.Equal(3, (await db.UserCards.SingleAsync()).QuantityOwned);
        Assert.Equal(firstId, (await db.DeckCards.SingleAsync()).CardPrintingId);
    }

    [Fact]
    public void Track_ChangedSourceId_DoesNotResolveRelationshipsToStaleId()
    {
        var target = new CardPrinting
        {
            Number = "target",
            DetailsJson = """{"sourceId":1,"cardUid":"stable"}"""
        };
        var relationship = new CardPrinting
        {
            Number = "relationship",
            DetailsJson = """{"variantOfSourceId":1}"""
        };
        var identity = new SwuSharedIdentity([target, relationship]);

        target.DetailsJson = """{"sourceId":101,"cardUid":"stable"}""";
        identity.Track(target);
        identity.ResolveRelationships(out _);

        Assert.Null(relationship.VariantOf);
        Assert.Null(relationship.VariantOfId);
    }

    [Fact]
    public async Task Import_VariantAndReprintTargets_ResolveIndependentlyAcrossSyncs()
    {
        await using var database = await Database.CreateAsync();
        await ImportAsync(database, Record(200, "Variant", "SOR", "variant", variantId: 100, reprintId: 300));
        await using (var unresolved = database.Open())
        {
            var printing = await unresolved.CardPrintings.SingleAsync();
            Assert.Null(printing.VariantOfId);
            Assert.Null(printing.ReprintOfId);
            using var details = JsonDocument.Parse(printing.DetailsJson!);
            Assert.Equal("uid-100", details.RootElement.GetProperty("variantOfCardUid").GetString());
            Assert.Equal("uid-300", details.RootElement.GetProperty("reprintOfCardUid").GetString());
            Assert.False(details.RootElement.TryGetProperty("variantOfSourceId", out _));
            Assert.False(details.RootElement.TryGetProperty("reprintOfSourceId", out _));
        }
        await ImportAsync(database, Record(300, "Reprint target", "SHD", "reprint"));
        await using (var partial = database.Open())
        {
            var printing = await PrintingWithSourceId(partial, 200)
                .Include(p => p.ReprintOf).SingleAsync();
            Assert.Null(printing.VariantOfId);
            Assert.Equal("uid-300", printing.ReprintOf!.CardUid);
        }
        await ImportAsync(database, Record(100, "Variant target", "SOR", "base"));
        await using var db = database.Open();
        var resolved = await PrintingWithSourceId(db, 200).Include(p => p.Card)
            .Include(p => p.VariantOf).Include(p => p.ReprintOf).SingleAsync();
        Assert.Equal("uid-100", resolved.VariantOf!.CardUid);
        Assert.Equal("uid-300", resolved.ReprintOf!.CardUid);
        Assert.Equal(resolved.VariantOf.Id, resolved.VariantOfId);
        Assert.Equal(resolved.ReprintOf.Id, resolved.ReprintOfId);
        Assert.NotEqual(resolved.VariantOfId, resolved.ReprintOfId);
    }

    [Fact]
    public async Task Import_LogicalCardKey_GroupsPrintingsByTitleAndSubtitleAcrossPrintings()
    {
        await using var database = await Database.CreateAsync();
        await ImportAsync(database, Record(1, "Original", "SOR", "one", uid: "000-text-uid"));
        var result = await ImportAsync(database,
            Record(2, "Renamed", "SHD", "two", uid: "uid-2"),
            Record(3, "Renamed", "SHD", "three", uid: "different"));
        Assert.Equal(0, result.Errors);
        Assert.Equal(1, result.CardsCreated);
        await using var db = database.Open();
        var printings = (await db.CardPrintings.Include(p => p.Card).ToListAsync())
            .OrderBy(SourceId).ToArray();
        Assert.Equal(2, await db.Cards.CountAsync());
        Assert.NotEqual(printings[0].CardId, printings[1].CardId);
        Assert.Equal(printings[1].CardId, printings[2].CardId);
        Assert.Equal("uid-2", printings[1].Card.CardUid);
        Assert.Equal("Renamed", printings[1].Card.Name);
        Assert.Equal(new[] { 1, 2, 3 }, printings.Select(SourceId));
    }

    [Fact]
    public async Task Import_NormalizedFields_ReplaysAndReplacesAssociationsWithoutDuplicates()
    {
        await using var database = await Database.CreateAsync();
        var record = JsonSerializer.SerializeToNode(Record(1, "Double aspect", "SOR", "one"))!;
        var attrs = record["attributes"]!;
        attrs["text"] = "Rules";
        attrs["textStyled"] = "<p>Rules</p>";
        attrs["deployBox"] = "On Attack";
        attrs["deployBoxStyled"] = "<p>On Attack</p>";
        attrs["epicAction"] = "Deploy";
        attrs["epicActionStyled"] = "<p>Deploy</p>";
        attrs["rules"] = "Reminder";
        attrs["rulesStyled"] = "<p>Reminder</p>";
        attrs["health"] = 7;
        attrs["type"] = JsonNode.Parse("""{"data":{"id":1,"attributes":{"name":"Leader","value":"leader"}}}""");
        attrs["type2"] = JsonNode.Parse("""{"data":{"id":2,"attributes":{"name":"Unit","value":"unit"}}}""");
        attrs["aspects"] = JsonNode.Parse("""{"data":[{"id":1,"attributes":{"name":"Command"}}]}""");
        attrs["aspectDuplicates"] = attrs["aspects"]!.DeepClone();
        attrs["traits"] = JsonNode.Parse("""["Imperial","Imperial"]""");
        attrs["keywords"] = JsonNode.Parse("""["Sentinel"]""");
        attrs["arenas"] = JsonNode.Parse("""{"data":[{"id":1,"attributes":{"name":"Ground"}}]}""");
        attrs["variantTypes"] = JsonNode.Parse("""{"data":[{"id":1,"attributes":{"name":"Hyperspace","variantId":"03","foil":false,"icon":{"data":{"id":8,"attributes":{"url":"https://example.invalid/hyperspace.svg"}}}}}]}""");
        attrs["artFront"] = JsonNode.Parse("""{"data":{"id":1,"attributes":{"url":"https://example.invalid/front.png"}}}""");
        attrs["artBack"] = JsonNode.Parse("""{"data":{"id":2,"attributes":{"url":"https://example.invalid/back.png"}}}""");
        attrs["artFrontHorizontal"] = true;
        attrs["artBackHorizontal"] = false;
        attrs["createdAt"] = "2024-01-01T01:00:00Z";
        attrs["updatedAt"] = "2024-02-01T01:00:00Z";
        attrs["publishedAt"] = "2024-03-01T01:00:00Z";
        attrs["expansion"]!["data"]!["attributes"]!["releaseDate"] = "2024-03-08";

        Assert.Equal(0, (await ImportAsync(database, record)).Errors);
        var replay = await ImportAsync(database, record);
        Assert.Equal(0, replay.Errors);
        Assert.Equal(0, replay.CardsCreated + replay.CardsUpdated + replay.PrintingsCreated + replay.PrintingsUpdated);
        await using (var db = database.Open())
        {
            var card = await db.Cards.Include(c => c.CardTypes).ThenInclude(t => t.CardType)
                .Include(c => c.CardAspects).SingleAsync();
            Assert.Equal("Rules", card.Text);
            Assert.Equal("<p>Rules</p>", card.TextStyled);
            Assert.Equal("On Attack", card.DeployBox);
            Assert.Equal("<p>On Attack</p>", card.DeployBoxStyled);
            Assert.Equal("Deploy", card.EpicAction);
            Assert.Equal("<p>Deploy</p>", card.EpicActionStyled);
            Assert.Equal("Reminder", card.Rules);
            Assert.Equal("<p>Reminder</p>", card.RulesStyled);
            Assert.Equal(7, card.Hp);
            Assert.Equal(new[] { ("Leader", "Type"), ("Unit", "Type2") },
                card.CardTypes.OrderBy(t => t.SourceRole).Select(t => (t.CardType.Name, t.SourceRole)));
            Assert.Equal(2, Assert.Single(card.CardAspects).Count);
            Assert.Single(await db.Set<CardTrait>().ToListAsync());
            Assert.Single(await db.Set<CardKeyword>().ToListAsync());
            Assert.Single(await db.Set<CardArena>().ToListAsync());
            var printing = await db.CardPrintings.Include(p => p.CatalogSet).Include(p => p.Treatment).SingleAsync();
            Assert.Equal("Hyperspace", printing.Treatment!.Name);
            Assert.Equal("https://example.invalid/hyperspace.svg", printing.Treatment.IconUrl);
            Assert.False(printing.IsFoil);
            Assert.Equal("https://example.invalid/front.png", printing.ImageUrl);
            Assert.Equal("https://example.invalid/back.png", printing.BackImageUrl);
            Assert.True(printing.ArtFrontHorizontal);
            Assert.False(printing.ArtBackHorizontal);
            Assert.Equal(DateTimeOffset.Parse("2024-01-01T01:00:00Z"), printing.ApiCreatedAt);
            Assert.Equal(DateTimeOffset.Parse("2024-02-01T01:00:00Z"), printing.ApiUpdatedAt);
            Assert.Equal(DateTimeOffset.Parse("2024-03-01T01:00:00Z"), printing.ApiPublishedAt);
            Assert.Equal(new DateOnly(2024, 3, 8), printing.CatalogSet!.ReleasedAt);
        }

        attrs["aspectDuplicates"] = null;
        attrs["type2"] = null;
        attrs["traits"] = JsonNode.Parse("""["Rebel"]""");
        attrs["keywords"] = null;
        attrs["arenas"] = null;
        attrs["artFront"] = null;
        attrs["health"] = null;
        attrs["hp"] = 8;
        attrs["hyperspace"] = true;
        attrs["showcase"] = true;
        Assert.Equal(0, (await ImportAsync(database, record)).Errors);
        await using var verify = database.Open();
        Assert.Single(await verify.CardTypes.ToListAsync(), t => t.Name == "Leader");
        Assert.Single(await verify.Set<CardCardType>().ToListAsync());
        Assert.Equal(1, (await verify.Set<CardAspect>().SingleAsync()).Count);
        Assert.Equal("Rebel", (await verify.Set<CardTrait>().Include(t => t.Trait).SingleAsync()).Trait.Name);
        Assert.Empty(await verify.Set<CardKeyword>().ToListAsync());
        Assert.Empty(await verify.Set<CardArena>().ToListAsync());
        Assert.Null((await verify.CardPrintings.SingleAsync()).ImageUrl);
        Assert.Equal(8, (await verify.Cards.SingleAsync()).Hp);
        var changedPrinting = await verify.CardPrintings.Include(p => p.Treatment).SingleAsync();
        Assert.Equal("Hyperspace", changedPrinting.Treatment!.Name);
    }

    [Fact]
    public async Task Import_ArenaOnlyChange_CountsCardAsUpdated()
    {
        await using var database = await Database.CreateAsync();
        var original = JsonSerializer.SerializeToNode(Record(1, "Arena card", "SOR", "one"))!;
        original["attributes"]!["arenas"] =
            JsonNode.Parse("""{"data":[{"id":1,"attributes":{"name":"Ground"}}]}""");
        Assert.Equal(0, (await ImportAsync(database, original)).Errors);

        var changed = original.DeepClone();
        changed["attributes"]!["arenas"] =
            JsonNode.Parse("""{"data":[{"id":2,"attributes":{"name":"Space"}}]}""");
        var result = await ImportAsync(database, changed);

        Assert.Equal(1, result.CardsUpdated);
        await using var db = database.Open();
        var card = await db.Cards.Include(c => c.CardArenas).ThenInclude(a => a.Arena).SingleAsync();
        Assert.Equal("Space", Assert.Single(card.CardArenas).Arena.Name);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Import_ReplayAndRename_PreservesIdentityAndReferences(bool serialChanges)
    {
        await using var database = await Database.CreateAsync();
        var original = Record(1, "Original", "SOR", "serial-1", uid: "stable-uid");
        await ImportAsync(database, original);
        int cardId;
        int printingId;
        await using (var db = database.Open())
        {
            var printing = await db.CardPrintings.SingleAsync();
            cardId = printing.CardId;
            printingId = printing.Id;
            var user = new User { Username = "owner", DisplayName = "Owner" };
            var deck = new Deck { User = user, Name = "Deck", Game = "Star Wars Unlimited" };
            db.UserCards.Add(new UserCard
            {
                User = user,
                CardPrintingId = printingId,
                QuantityOwned = 3
            });
            db.DeckCards.Add(new DeckCard
            {
                Deck = deck,
                CardPrintingId = printingId,
                QuantityInDeck = 2
            });
            await db.SaveChangesAsync();
        }

        var replay = await ImportAsync(database, original);
        Assert.Equal(0, replay.Errors);
        Assert.Equal(0, replay.CardsCreated + replay.PrintingsCreated
            + replay.CardsUpdated + replay.PrintingsUpdated);

        // A stable printing UID survives changes to the source ID and serial code.
        var renamed = Record(serialChanges ? 1 : 101, "Renamed", "SHD",
            serialChanges ? "corrected-serial" : "serial-1", subtitle: "Corrected subtitle", uid: "stable-uid");
        var result = await ImportAsync(database, renamed);
        Assert.Equal(0, result.Errors);
        Assert.Equal(0, result.CardsCreated + result.PrintingsCreated);
        await using var verify = database.Open();
        var card = await verify.Cards.SingleAsync();
        var updated = await verify.CardPrintings.SingleAsync();
        Assert.Equal(cardId, card.Id);
        Assert.Equal("Renamed — Corrected subtitle", card.Name);
        Assert.Equal(printingId, updated.Id);
        Assert.Equal(cardId, updated.CardId);
        Assert.Equal(serialChanges ? "1" : "101", updated.Number);
        var owned = await verify.UserCards.SingleAsync();
        Assert.Equal(printingId, owned.CardPrintingId);
        Assert.Equal(3, owned.QuantityOwned);
        var deckCard = await verify.DeckCards.SingleAsync();
        Assert.Equal(printingId, deckCard.CardPrintingId);
        Assert.Equal(2, deckCard.QuantityInDeck);
    }

    [Fact]
    public async Task Import_SerialCodeDoesNotPopulatePrintingNumber()
    {
        await using var database = await Database.CreateAsync();
        await ImportAsync(database, Record(1, "Original", "SOR", "old-serial"));
        await ImportAsync(database, Record(1, "Renamed", "SOR", "new-serial"));

        var result = await ImportAsync(database, Record(2, "Distinct card", "SHD", "old-serial"));

        Assert.Equal(0, result.Errors);
        await using var db = database.Open();
        Assert.Equal(2, await db.Cards.CountAsync());
        Assert.Equal(2, await db.CardPrintings.CountAsync());
        Assert.Equal(new[] { "1", "2" }, await db.CardPrintings.OrderBy(printing => printing.Id)
            .Select(printing => printing.Number).ToArrayAsync());
    }

    [Fact]
    public async Task Import_DuplicatePayload_UsesTrackedPrintingAndSharedCanonicalCard()
    {
        await using var database = await Database.CreateAsync();
        var first = Record(1, "Han Solo", "SOR", "standard");
        var variant = Record(2, "Han Solo", "SHD", "showcase", variantId: 1, uid: "uid-2");
        var summary = await ImportAsync(database, first, first, variant, variant);
        Assert.Equal(0, summary.Errors);
        Assert.Equal(1, summary.CardsCreated);
        Assert.Equal(0, summary.CardsUpdated);
        Assert.Equal(2, summary.PrintingsCreated);
        await using var db = database.Open();
        var card = await db.Cards.SingleAsync();
        Assert.Equal(2, await db.CardPrintings.CountAsync());
        Assert.All(await db.CardPrintings.ToListAsync(), p => Assert.Equal(card.Id, p.CardId));
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task Import_VariantBeforeBase_ResolvesStableIdentityAcrossSets(
        bool separateImports, bool useUid)
    {
        await using var database = await Database.CreateAsync();
        var variant = Record(2, "Luke Skywalker", "SHD", "variant", subtitle: "Showcase",
            variantId: useUid ? 999 : 1, variantUid: "uid-1", reprintId: 3);
        // This title deliberately matches the relationship's title but not its identity.
        var impostor = Record(3, "Luke Skywalker", "SOR", "impostor", subtitle: "Wrong");
        var basis = Record(1, "Luke Skywalker", "SOR", "base", subtitle: "Jedi Knight");
        await ImportAsync(database, variant, impostor);
        await using (var before = database.Open())
        {
            Assert.Null((await PrintingWithSourceId(before, 2).SingleAsync()).VariantOfId);
        }
        if (separateImports)
            await ImportAsync(database, basis);
        else
            await ImportAsync(database, variant, basis);

        await using var db = database.Open();
        var cards = await db.Cards.ToListAsync();
        var baseCard = cards.Single(c => c.Name.EndsWith("Jedi Knight"));
        var basePrinting = await db.CardPrintings.SingleAsync(p => p.CardId == baseCard.Id);
        var variantPrinting = await PrintingWithSourceId(db, 2).SingleAsync();
        Assert.Equal(basePrinting.Id, variantPrinting.VariantOfId);
        using var metadata = JsonDocument.Parse(
            (await PrintingWithSourceId(db, 2).SingleAsync()).DetailsJson!);
        Assert.Equal("uid-1", metadata.RootElement.GetProperty("variantOfCardUid").GetString());
        Assert.Equal("uid-3", metadata.RootElement.GetProperty("reprintOfCardUid").GetString());
        Assert.False(metadata.RootElement.TryGetProperty("variantOfSourceId", out _));
        Assert.False(metadata.RootElement.TryGetProperty("reprintOfSourceId", out _));
    }

    [Fact]
    public async Task Import_ReprintOnlyRelationship_ResolvesWhenBaseArrivesLater()
    {
        await using var database = await Database.CreateAsync();
        await ImportAsync(database, Record(2, "Reprint", "SHD", "two", reprintId: 1));
        await ImportAsync(database, Record(1, "Base", "SOR", "one"));

        await using var db = database.Open();
        var baseCard = await db.Cards.SingleAsync(card => card.Name == "Base");
        var basePrinting = await db.CardPrintings.SingleAsync(printing => printing.CardId == baseCard.Id);
        var reprintPrinting = await PrintingWithSourceId(db, 2).SingleAsync();
        Assert.Equal(basePrinting.Id, reprintPrinting.ReprintOfId);
    }

    [Fact]
    public async Task Import_SamePayloadVariantBeforeBase_ResolvesTemporaryEntityKeys()
    {
        await using var database = await Database.CreateAsync();
        var result = await ImportAsync(database,
            Record(2, "Leia", "SHD", "variant", subtitle: "Showcase", variantId: 1),
            Record(1, "Leia", "SOR", "base", subtitle: "Rebel"));
        Assert.Equal(0, result.Errors);
        Assert.Equal(2, result.CardsCreated);
        Assert.Equal(0, result.CardsUpdated);
        await using var db = database.Open();
        var basis = await db.Cards.SingleAsync(c => c.Name == "Leia — Rebel");
        var basisPrinting = await db.CardPrintings.SingleAsync(printing => printing.CardId == basis.Id);
        var variantPrinting = await PrintingWithSourceId(db, 2).SingleAsync();
        Assert.Equal(basisPrinting.Id, variantPrinting.VariantOfId);
    }

    [Fact]
    public async Task Import_SharedCardMetadataOverwritten_RetainsPendingPrintingRelationship()
    {
        await using var database = await Database.CreateAsync();
        await ImportAsync(database,
            Record(2, "Leia", "SHD", "variant", subtitle: "Showcase", variantId: 1));
        await ImportAsync(database,
            Record(3, "Leia", "TWI", "another-printing", subtitle: "Showcase", uid: "uid-3"));
        await ImportAsync(database,
            Record(1, "Leia", "SOR", "base", subtitle: "Rebel"));

        await using var db = database.Open();
        var basis = await db.Cards.SingleAsync(c => c.Name == "Leia — Rebel");
        var variant = await db.Cards.SingleAsync(c => c.Name == "Leia — Showcase");
        var basisPrinting = await db.CardPrintings.SingleAsync(printing => printing.CardId == basis.Id);
        var variantPrinting = await PrintingWithSourceId(db, 2).SingleAsync();
        Assert.Equal(basisPrinting.Id, variantPrinting.VariantOfId);
        Assert.Equal(2, await db.CardPrintings.CountAsync(p => p.CardId == variant.Id));
    }

    [Fact]
    public async Task Import_DeferredRelationshipRepair_CountsExistingCardInDryRunAndCommit()
    {
        await using var database = await Database.CreateAsync();
        await ImportAsync(database, Record(2, "Variant", "SHD", "two", variantId: 1));
        var basis = Record(1, "Base", "SOR", "one");
        await using (var db = database.Open())
        {
            await using var stream = Payload(basis);
            var preview = await new SwuDbImporter(db, new UnusedClient(), NullLogger<SwuDbImporter>.Instance).ImportFromFileAsync(
                stream, new ImportOptions(DryRun: true));
            Assert.Equal(1, preview.CardsCreated);
            Assert.Equal(1, preview.PrintingsUpdated);
        }
        await AssertBaseAsync(database, "Variant", null);
        var committed = await ImportAsync(database, basis);
        Assert.Equal(1, committed.CardsCreated);
        Assert.Equal(1, committed.PrintingsUpdated);
        await AssertBaseAsync(database, "Variant", "Base");
        var replay = await ImportAsync(database, basis);
        Assert.Equal(0, replay.CardsCreated + replay.CardsUpdated + replay.PrintingsCreated + replay.PrintingsUpdated);
    }

    [Fact]
    public async Task Import_MetadataAndRelationshipChange_CountsExistingPrintingOnce()
    {
        await using var database = await Database.CreateAsync();
        await ImportAsync(database,
            Record(1, "Base", "SOR", "one"),
            Record(2, "Variant", "SHD", "two"));
        var updated = Record(2, "Variant", "SHD", "two", variantId: 1);
        var result = await ImportAsync(database, updated, updated);
        Assert.Equal(0, result.CardsCreated);
        Assert.Equal(0, result.CardsUpdated);
        Assert.Equal(1, result.PrintingsUpdated);
        await AssertBaseAsync(database, "Variant", "Base");
    }

    [Fact]
    public async Task Import_ChangedRemovedAndDeferredRelationships_RepairsStaleLinks()
    {
        await using var database = await Database.CreateAsync();
        await ImportAsync(database,
            Record(1, "First base", "SOR", "one"),
            Record(2, "Second base", "SHD", "two"),
            Record(3, "Variant", "SHD", "three", variantId: 1));
        await AssertBaseAsync(database, "Variant", "First base");

        await ImportAsync(database, Record(3, "Variant", "SHD", "three", variantId: 2));
        await AssertBaseAsync(database, "Variant", "Second base");
        await ImportAsync(database, Record(3, "Variant", "SHD", "three"));
        await AssertBaseAsync(database, "Variant", null);
        await ImportAsync(database, Record(3, "Variant", "SHD", "three", variantId: 1));
        await AssertBaseAsync(database, "Variant", "First base");
        await ImportAsync(database, Record(3, "Variant", "SHD", "three", variantId: 4));
        await AssertBaseAsync(database, "Variant", null);
        await ImportAsync(database, Record(4, "Later base", "TWI", "four"));
        await AssertBaseAsync(database, "Variant", "Later base");
    }

    [Fact]
    public async Task Import_RemovedSourceRelationship_TakesPriorityOverStaleResolvedId()
    {
        await using var database = await Database.CreateAsync();
        await ImportAsync(database, Record(99, "Target", "TWI", "target"));
        await ImportAsync(database, Record(1, "Shared", "SOR", "lower", variantId: 99));
        var originalBase = JsonSerializer.SerializeToNode(Record(2, "Shared", "SHD", "upper"))!;
        originalBase["attributes"]!["text"] = "original base";
        await ImportAsync(database, originalBase);

        var lower = JsonSerializer.SerializeToNode(Record(1, "Shared", "SOR", "lower"))!;
        lower["attributes"]!["text"] = "lower source";
        var higher = JsonSerializer.SerializeToNode(Record(2, "Shared", "SHD", "upper"))!;
        higher["attributes"]!["text"] = "higher source";
        await ImportAsync(database, lower, higher);

        await using (var db = database.Open())
            Assert.Equal("lower source", (await db.Cards.SingleAsync(card => card.Name == "Shared")).Text);

        var replay = await ImportAsync(database, lower, higher);
        Assert.Equal(0, replay.CardsCreated + replay.CardsUpdated);
        await using var verify = database.Open();
        Assert.Equal("lower source", (await verify.Cards.SingleAsync(card => card.Name == "Shared")).Text);
    }

    [Fact]
    public async Task Import_RemovedRelationship_PreservesOtherCanonicalPrintingLink()
    {
        await using var database = await Database.CreateAsync();
        await ImportAsync(database,
            Record(1, "Base", "SOR", "one"),
            Record(2, "Variant", "SHD", "two", variantId: 1),
            Record(3, "Variant", "TWI", "three", variantId: 1, uid: "uid-3"));
        await ImportAsync(database, Record(2, "Variant", "SHD", "two"));
        await AssertBaseAsync(database, "Variant", "Base");
        await ImportAsync(database, Record(3, "Variant", "TWI", "three", uid: "uid-3"));
        await AssertBaseAsync(database, "Variant", null);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public async Task Import_MissingLocale_ReportsErrorInsteadOfSkipping(string? locale)
    {
        await using var database = await Database.CreateAsync();
        var result = await ImportAsync(database, Record(1, "Invalid", "SOR", "one", locale: locale));
        Assert.Equal(1, result.Errors);
        Assert.Equal(0, result.CardsCreated + result.PrintingsCreated);
        await using var db = database.Open();
        Assert.Empty(await db.Cards.ToListAsync());
        Assert.Empty(await db.CardPrintings.ToListAsync());
    }

    [Fact]
    public async Task Import_InvalidRecord_ReportsErrorWithoutPartialChanges()
    {
        await using var database = await Database.CreateAsync();
        await ImportAsync(database, Record(1, "Valid", "SOR", "one"));
        var result = await ImportAsync(database,
            Record(1, " ", "SHD", "changed"),
            new { id = 2, attributes = (object?)null },
            Record(3, "Next", "SOR", "three"));
        Assert.Equal(2, result.Errors);
        Assert.Equal(1, result.CardsCreated);
        Assert.Equal(1, result.PrintingsCreated);
        await using var db = database.Open();
        Assert.Equal(2, await db.Cards.CountAsync());
        Assert.Equal("Valid", (await PrintingWithSourceId(db, 1).Include(p => p.Card).SingleAsync()).Card.Name);
        Assert.False(await db.CardPrintings.AnyAsync(p => p.DetailsJson != null
            && p.DetailsJson.Contains("\"sourceId\":1,") && p.CatalogSet!.Code == "SHD"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Import_PrintingConstraintFailure_RestoresFailedRecordAndContinues(bool newCard)
    {
        await using var database = await Database.CreateAsync();
        await ImportAsync(database,
            Record(1, "Shared", "SOR", "one"),
            Record(2, "Shared", "SOR", "two"));
        var failed = Record(3, newCard ? "New" : "Shared", "SOR", "three");
        // SQLite's trigger simulates a persistence failure after the entities have been changed.
        await using (var setup = database.Open())
        {
            await setup.Database.ExecuteSqlRawAsync("""
                CREATE TRIGGER RejectPrintingInsert BEFORE INSERT ON CardPrintings
                WHEN NEW.Number = '3' BEGIN SELECT RAISE(ABORT, 'Rejected printing'); END;
                """);
        }
        var result = await ImportAsync(database, failed, Record(4, "Next", "SHD", "four"));
        Assert.Equal(1, result.Errors);
        Assert.Contains(result.Messages, message => message.Contains("Record could not be imported."));
        Assert.DoesNotContain(result.Messages, message =>
            message.Contains("Rejected printing") || message.Contains("UNIQUE constraint failed"));
        Assert.Equal(1, result.CardsCreated);
        Assert.Equal(1, result.PrintingsCreated);
        await using var db = database.Open();
        Assert.Equal(2, await db.Cards.CountAsync());
        Assert.True(await db.Cards.AnyAsync(c => c.Name == "Shared"));
        Assert.False(await db.Cards.AnyAsync(c => c.Name == "Changed" || c.Name == "New"));
        Assert.Equal(3, await db.CardPrintings.CountAsync());
        Assert.Equal(1, await db.CardPrintings.CountAsync(p => p.CardUid == "uid-1"));
        Assert.Equal(1, await db.CardPrintings.CountAsync(p => p.CardUid == "uid-2"));
    }

    [Fact]
    public async Task Import_RecordFailure_LogsExceptionAndSourceId_AndKeepsMessageGeneric()
    {
        await using var database = await Database.CreateAsync();
        await using (var setup = database.Open())
        {
            await setup.Database.ExecuteSqlRawAsync("""
                CREATE TRIGGER RejectPrintingInsert BEFORE INSERT ON CardPrintings
                WHEN NEW.Number = '71' BEGIN SELECT RAISE(ABORT, 'Rejected printing'); END;
                """);
        }

        var loggerProvider = new TestLoggerProvider();
        using var loggerFactory = LoggerFactory.Create(builder => builder.AddProvider(loggerProvider));
        await using var db = database.Open();
        await using var stream = Payload(Record(71, "Rejected", "SOR", "failed"));
        var summary = await new SwuDbImporter(db, new UnusedClient(),
                loggerFactory.CreateLogger<SwuDbImporter>())
            .ImportFromFileAsync(stream, new ImportOptions(DryRun: false));

        Assert.Equal(1, summary.Errors);
        Assert.Contains(summary.Messages, message => message.Contains("Record could not be imported."));
        Assert.DoesNotContain(summary.Messages, message => message.Contains("Rejected printing"));
        var log = Assert.Single(loggerProvider.Entries, entry => entry.Level == LogLevel.Error);
        Assert.NotNull(log.Exception);
        Assert.Contains(log.State, item => item.Key == "SourceId" && Equals(item.Value, 71));
    }

    [Fact]
    public async Task Import_FullCatalog_ImportsAllExpansionsAndPages_Idempotently()
    {
        await using var database = await Database.CreateAsync();
        using var handler = new PageHandler("none");
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://example.invalid/api/") };
        int[] printingIds = [];
        for (int run = 0; run < 2; run++)
        {
            await using var db = database.Open();
            Assert.Equal(run == 0 ? 0 : 2, await db.Sets.CountAsync());
            var importer = new SwuDbImporter(db, new SWUApiClient(http), NullLogger<SwuDbImporter>.Instance);
            var summary = await importer.ImportFromRemoteAsync(new ImportOptions(DryRun: false));

            Assert.Equal(0, summary.Errors);
            Assert.Equal(run == 0 ? 2 : 0, summary.CardsCreated);
            Assert.Equal(run == 0 ? 2 : 0, summary.PrintingsCreated);
            Assert.Equal(2, await db.Cards.CountAsync());
            var printings = await db.CardPrintings.Include(p => p.CatalogSet)
                .OrderBy(p => p.Id).ToArrayAsync();
            Assert.Equal(["SOR", "NEW"], printings.Select(p => p.CatalogSet!.Code).ToArray());
            if (run == 0)
                printingIds = printings.Select(p => p.Id).ToArray();
            else
                Assert.Equal(printingIds, printings.Select(p => p.Id).ToArray());
        }

        Assert.Equal([1, 2, 1, 2], handler.Requests
            .Select(uri => int.Parse(HttpUtility.ParseQueryString(uri.Query)["pagination[page]"]!)).ToArray());
        Assert.All(handler.Requests, uri =>
        {
            Assert.EndsWith("/card-list", uri.AbsolutePath);
            var query = HttpUtility.ParseQueryString(uri.Query);
            Assert.Equal("en", query["locale"]);
            Assert.DoesNotContain(query.AllKeys, key => key?.StartsWith("filters[", StringComparison.Ordinal) == true);
        });
    }

    [Theory]
    [InlineData("http", "SOR")]
    [InlineData("missing-data", "SOR")]
    [InlineData("missing-pagination", "SOR")]
    [InlineData("wrong-page", "SOR")]
    [InlineData("truncated", "SOR")]
    [InlineData("duplicate", "SOR")]
    [InlineData("http", null)]
    [InlineData("missing-data", null)]
    [InlineData("missing-pagination", null)]
    [InlineData("wrong-page", null)]
    [InlineData("truncated", null)]
    [InlineData("duplicate", null)]
    public async Task Import_FailedLaterApiPage_WritesNothing(string failure, string? setCode)
    {
        await using var database = await Database.CreateAsync();
        using var http = new HttpClient(new PageHandler(failure))
        {
            BaseAddress = new Uri("https://example.invalid/api/")
        };
        await using (var db = database.Open())
        {
            var importer = new SwuDbImporter(db, new SWUApiClient(http), NullLogger<SwuDbImporter>.Instance);
            await Assert.ThrowsAnyAsync<Exception>(() => importer.ImportFromRemoteAsync(
                new ImportOptions(DryRun: false, SetCode: setCode)));
            Assert.Empty(db.ChangeTracker.Entries());
        }
        await using var verify = database.Open();
        Assert.Empty(await verify.Cards.ToListAsync());
        Assert.Empty(await verify.CardPrintings.ToListAsync());
    }

    [Fact]
    public async Task Import_CancellationWhileSaving_IsNotCountedAsRecordError()
    {
        await using var database = await Database.CreateAsync();
        await using (var db = database.Open(new CancelSaveInterceptor()))
        {
            await using var stream = Payload(Record(1, "Cancelled", "SOR", "one"));
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                new SwuDbImporter(db, new UnusedClient(), NullLogger<SwuDbImporter>.Instance).ImportFromFileAsync(stream,
                    new ImportOptions(DryRun: false)));
        }
        await using var verify = database.Open();
        Assert.Empty(await verify.Cards.ToListAsync());
        Assert.Empty(await verify.CardPrintings.ToListAsync());
    }

    [Fact]
    public async Task Import_Cancellation_PropagatesWithoutWrites()
    {
        await using var database = await Database.CreateAsync();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await using (var db = database.Open())
        {
            await using var stream = Payload(Record(1, "Cancelled", "SOR", "one"));
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                new SwuDbImporter(db, new UnusedClient(), NullLogger<SwuDbImporter>.Instance).ImportFromFileAsync(stream,
                    new ImportOptions(DryRun: false), cancellation.Token));
        }
        await using var verify = database.Open();
        Assert.Empty(await verify.Cards.ToListAsync());
    }

    private static IQueryable<CardPrinting> PrintingWithSourceId(AppDbContext db, int sourceId)
        => db.CardPrintings.Where(printing => printing.DetailsJson != null
            && printing.DetailsJson.Contains($"\"sourceId\":{sourceId},"));

    private static int SourceId(CardPrinting printing)
    {
        using var details = JsonDocument.Parse(printing.DetailsJson!);
        return details.RootElement.GetProperty("sourceId").GetInt32();
    }

    private static object Record(int id, string title, string set, string serial,
        string? subtitle = null, int? variantId = null, string? variantUid = null,
        int? reprintId = null, string? locale = "en", string? uid = null)
        => new
        {
            id,
            attributes = new
            {
                title,
                subtitle,
                locale,
                serialCode = serial,
                cardUid = uid ?? $"uid-{id}",
                cardNumber = id,
                rarity = "Common",
                expansion = new { data = new { id = 1, attributes = new { code = set, name = set } } },
                variantOf = variantId is null ? null : new
                {
                    data = new
                    {
                        id = variantId.Value,
                        attributes = new { title = "Luke Skywalker — Wrong", cardUid = variantUid ?? $"uid-{variantId}" }
                    }
                },
                reprintOf = reprintId is null ? null : new
                {
                    data = new { id = reprintId.Value, attributes = new { cardUid = $"uid-{reprintId}" } }
                }
            }
        };

    private static MemoryStream Payload(params object[] records)
        => new(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(records)));

    private static async Task AssertBaseAsync(Database database, string name, string? baseName)
    {
        await using var db = database.Open();
        var printings = await db.CardPrintings.Include(p => p.VariantOf!).ThenInclude(p => p.Card)
            .Where(p => p.Card!.Name == name).ToListAsync();
        Assert.NotEmpty(printings);
        var targets = printings.Where(p => p.VariantOf is not null)
            .Select(p => p.VariantOf!.Card.Name).Distinct().ToArray();
        if (baseName is null)
            Assert.Empty(targets);
        else
            Assert.Equal([baseName], targets);
    }

    private static async Task<ImportSummary> ImportAsync(Database database, params object[] records)
    {
        await using var db = database.Open();
        await using var stream = Payload(records);
        return await new SwuDbImporter(db, new UnusedClient(), NullLogger<SwuDbImporter>.Instance).ImportFromFileAsync(stream,
            new ImportOptions(DryRun: false));
    }

    private sealed class Database(SqliteConnection connection) : IAsyncDisposable
    {
        internal AppDbContext Open(params IInterceptor[] interceptors)
            => new(new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(connection).AddInterceptors(interceptors).Options);

        internal static async Task<Database> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var database = new Database(connection);
            await using var db = database.Open();
            await db.Database.EnsureCreatedAsync();
            return database;
        }

        public ValueTask DisposeAsync() => connection.DisposeAsync();
    }

    private sealed class CancelSaveInterceptor : SaveChangesInterceptor
    {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result,
            CancellationToken cancellationToken = default) => throw new OperationCanceledException();
    }

    private sealed class UnusedClient : ISWUApiClient
    {
        public Task<IReadOnlyList<StrapiRecord>> GetAllCardsAsync(SWUCardFilter filter,
            CancellationToken ct = default) => throw new NotSupportedException();

        public Task<int?> TryResolveExpansionIdAsync(string code, CancellationToken ct = default)
            => throw new NotSupportedException();
    }

    private sealed class PageHandler(string failure) : HttpMessageHandler
    {
        internal List<Uri> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Requests.Add(request.RequestUri!);
            var query = HttpUtility.ParseQueryString(request.RequestUri!.Query);
            var page = query["pagination[page]"];
            if (page == "2" && failure == "http")
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadGateway));
            string json = page == "2" ? failure switch
            {
                "none" => JsonSerializer.Serialize(new
                {
                    data = new[] { Record(2, "Second page", "NEW", "two") },
                    meta = new { pagination = new { page = 2, pageSize = 1, pageCount = 2, total = 2 } }
                }),
                "missing-data" => """{"meta":{"pagination":{"page":2,"pageSize":1,"pageCount":2,"total":2}}}""",
                "missing-pagination" => """{"data":[]}""",
                "wrong-page" => """{"data":[],"meta":{"pagination":{"page":1,"pageSize":1,"pageCount":2,"total":2}}}""",
                "duplicate" => JsonSerializer.Serialize(new
                {
                    data = new[] { Record(1, "Repeated page", "SOR", "one") },
                    meta = new { pagination = new { page = 2, pageSize = 1, pageCount = 2, total = 2 } }
                }),
                _ => """{"data":[],"meta":{"pagination":{"page":2,"pageSize":1,"pageCount":1,"total":2}}}"""
            } : JsonSerializer.Serialize(new
            {
                data = new[] { Record(1, "First page", "SOR", "one") },
                meta = new { pagination = new { page = 1, pageSize = 1, pageCount = 2, total = 2 } }
            });
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            });
        }
    }
}
