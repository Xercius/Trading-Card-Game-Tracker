using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using api.Common.Errors;
using api.Data;
using api.Middleware;
using api.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace api.Controllers
{
    [ApiController]
    [RequireUserHeader]
    [Route("api")]
    public class ImportExportController : ControllerBase
    {
        private readonly AppDbContext _db;
        public ImportExportController(AppDbContext db) => _db = db;

        // ------ DTOs ------
        public record ExportDeckCard(int CardPrintingId, int InDeck, int Idea, int Acquire, int Proxy);
        public record ExportDeck(string Game, string Name, string? Description, List<ExportDeckCard> Cards);
        public record ExportCollectionItem(int CardPrintingId, int QtyOwned, int QtyProxyOwned);
        public record ExportWishlistItem(int CardPrintingId, int Qty);
        public record ExportPayload(
            int Version,
            object? User,
            List<ExportCollectionItem> Collection,
            List<ExportWishlistItem> Wishlist,
            List<ExportDeck> Decks
        );

        public class ImportPayload
        {
            public int Version { get; set; } = 1;
            public List<ExportCollectionItem> Collection { get; set; } = new();
            public List<ExportWishlistItem> Wishlist { get; set; } = new();
            public List<ExportDeck> Decks { get; set; } = new();
        }

        private int? CurrentUserId() => HttpContext.GetCurrentUser()?.Id;

        // =========================
        // Issue 26: Export JSON
        // =========================
        [HttpGet("export/json")]
        public async Task<IActionResult> ExportJson()
        {
            var userId = CurrentUserId();
            if (userId is null)
            {
                return this.CreateProblem(
                    StatusCodes.Status400BadRequest,
                    title: "Missing required header",
                    detail: "The X-User-Id header is required.");
            }

            var collection = await _db.UserCards
                .Where(x => x.UserId == userId)
                .OrderBy(x => x.CardPrintingId)
                .Select(x => new ExportCollectionItem(x.CardPrintingId, x.QuantityOwned, x.QuantityProxyOwned))
                .ToListAsync();

            var wishlist = await _db.UserCards
                .Where(x => x.UserId == userId && x.QuantityWanted > 0)
                .OrderBy(x => x.CardPrintingId)
                .Select(x => new ExportWishlistItem(x.CardPrintingId, x.QuantityWanted))
                .ToListAsync();

            var deckEntities = await _db.Decks
                .Where(d => d.UserId == userId)
                .Include(d => d.Cards)
                .OrderBy(d => d.Game)
                .ThenBy(d => d.Name)
                .ToListAsync();

            var decks = deckEntities
                .Select(d => new ExportDeck(
                    d.Game,
                    d.Name,
                    d.Description,
                    d.Cards
                        .OrderBy(c => c.CardPrintingId)
                        .Select(c => new ExportDeckCard(
                            c.CardPrintingId,
                            c.QuantityInDeck,
                            c.QuantityIdea,
                            c.QuantityAcquire,
                            c.QuantityProxy))
                        .ToList()))
                .ToList();

            var payload = new ExportPayload(
                Version: 1,
                User: new { id = userId },
                Collection: collection,
                Wishlist: wishlist,
                Decks: decks
            );

            var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions
            {
                WriteIndented = true,
                DefaultIgnoreCondition = JsonIgnoreCondition.Never,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            var bytes = Encoding.UTF8.GetBytes(json);
            return File(bytes, "application/json", $"tcgtracker_export_user_{userId}_utc_{DateTime.UtcNow:yyyyMMddHHmmss}.json");
        }

        // =========================
        // Issue 27: CSV exports
        // =========================
        [HttpGet("export/collection.csv")]
        public async Task<IActionResult> ExportCollectionCsv()
        {
            var userId = CurrentUserId();
            if (userId is null)
            {
                return this.CreateProblem(
                    StatusCodes.Status400BadRequest,
                    title: "Missing required header",
                    detail: "The X-User-Id header is required.");
            }

            var rows = await _db.UserCards
                .Include(x => x.CardPrinting).ThenInclude(cp => cp.Card)
                .Where(x => x.UserId == userId)
                .OrderBy(r => r.CardPrinting.Card.Game)
                .ThenBy(r => r.CardPrinting.Card.Name)
                .ThenBy(r => r.CardPrinting.Set)
                .ThenBy(r => r.CardPrinting.Number)
                .Select(x => new
                {
                    Game = x.CardPrinting.Card.Game,
                    CardName = x.CardPrinting.Card.Name,
                    x.CardPrinting.Set,
                    x.CardPrinting.Number,
                    x.CardPrinting.Rarity,
                    x.CardPrinting.Style,
                    x.CardPrintingId,
                    x.QuantityOwned,
                    x.QuantityProxyOwned
                })
                .ToListAsync();

            var sb = new StringBuilder();
            sb.AppendLine("Game,CardName,Set,Number,Rarity,Style,CardPrintingId,QuantityOwned,QuantityProxyOwned");
            foreach (var r in rows)
            {
                sb.AppendLine($"{Csv(r.Game)},{Csv(r.CardName)},{Csv(r.Set)},{Csv(r.Number)},{Csv(r.Rarity)},{Csv(r.Style)},{r.CardPrintingId},{r.QuantityOwned},{r.QuantityProxyOwned}");
            }

            return File(Encoding.UTF8.GetBytes(sb.ToString()), "text/csv", "collection.csv");
        }

        [HttpGet("export/wishlist.csv")]
        public async Task<IActionResult> ExportWishlistCsv()
        {
            var userId = CurrentUserId();
            if (userId is null)
            {
                return this.CreateProblem(
                    StatusCodes.Status400BadRequest,
                    title: "Missing required header",
                    detail: "The X-User-Id header is required.");
            }

            var rows = await _db.UserCards
                .Include(x => x.CardPrinting).ThenInclude(cp => cp.Card)
                .Where(x => x.UserId == userId && x.QuantityWanted > 0)
                .OrderBy(r => r.CardPrinting.Card.Game)
                .ThenBy(r => r.CardPrinting.Card.Name)
                .ThenBy(r => r.CardPrinting.Set)
                .ThenBy(r => r.CardPrinting.Number)
                .Select(x => new
                {
                    Game = x.CardPrinting.Card.Game,
                    CardName = x.CardPrinting.Card.Name,
                    x.CardPrinting.Set,
                    x.CardPrinting.Number,
                    x.CardPrinting.Rarity,
                    x.CardPrinting.Style,
                    x.CardPrintingId,
                    Quantity = x.QuantityWanted
                })
                .ToListAsync();

            var sb = new StringBuilder();
            sb.AppendLine("Game,CardName,Set,Number,Rarity,Style,CardPrintingId,Quantity");
            foreach (var r in rows)
            {
                sb.AppendLine($"{Csv(r.Game)},{Csv(r.CardName)},{Csv(r.Set)},{Csv(r.Number)},{Csv(r.Rarity)},{Csv(r.Style)},{r.CardPrintingId},{r.Quantity}");
            }

            return File(Encoding.UTF8.GetBytes(sb.ToString()), "text/csv", "wishlist.csv");
        }

        [HttpGet("export/decks.csv")]
        public async Task<IActionResult> ExportDecksCsv()
        {
            var userId = CurrentUserId();
            if (userId is null)
            {
                return this.CreateProblem(
                    StatusCodes.Status400BadRequest,
                    title: "Missing required header",
                    detail: "The X-User-Id header is required.");
            }

            var rows = await _db.DeckCards
                .AsNoTracking()
                .Include(dc => dc.Deck)
                .Include(dc => dc.CardPrinting).ThenInclude(cp => cp.Card)
                .Where(dc =>
                    dc.Deck != null &&
                    dc.CardPrinting != null &&
                    dc.CardPrinting.Card != null &&
                    dc.Deck.UserId == userId)
                .OrderBy(r => r.Deck!.Game)
                .ThenBy(r => r.Deck!.Name)
                .ThenBy(r => r.CardPrinting!.Card!.Name)
                .ThenBy(r => r.CardPrinting!.Set ?? "")
                .ThenBy(r => r.CardPrinting!.Number ?? "")
                .Select(dc => new
                {
                    DeckName = dc.Deck!.Name,
                    Game = dc.Deck!.Game,
                    CardName = dc.CardPrinting!.Card!.Name,
                    Set = dc.CardPrinting!.Set ?? "",
                    Number = dc.CardPrinting!.Number ?? "",
                    Rarity = dc.CardPrinting!.Rarity ?? "",
                    Style = dc.CardPrinting!.Style ?? "",
                    dc.CardPrintingId,
                    dc.QuantityInDeck,
                    dc.QuantityIdea,
                    dc.QuantityAcquire,
                    dc.QuantityProxy
                })
                .ToListAsync();

            var sb = new StringBuilder();
            sb.AppendLine("DeckName,Game,CardName,Set,Number,Rarity,Style,CardPrintingId,QuantityInDeck,QuantityIdea,QuantityAcquire,QuantityProxy");
            foreach (var r in rows)
            {
                sb.AppendLine($"{Csv(r.DeckName)},{Csv(r.Game)},{Csv(r.CardName)},{Csv(r.Set)},{Csv(r.Number)},{Csv(r.Rarity)},{Csv(r.Style)},{r.CardPrintingId},{r.QuantityInDeck},{r.QuantityIdea},{r.QuantityAcquire},{r.QuantityProxy}");
            }

            return File(Encoding.UTF8.GetBytes(sb.ToString()), "text/csv", "decks.csv");
        }

        private static string Csv(string? s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            var needsQuotes = s.Contains(',') || s.Contains('"') || s.Contains('\n') || s.Contains('\r');
            var v = s.Replace("\"", "\"\"");
            return needsQuotes ? $"\"{v}\"" : v;
        }

        // =========================
        // Issue 28: Import JSON (merge/replace)
        // =========================
        [HttpPost("import/json")]
        public async Task<IActionResult> ImportJson([FromQuery] string mode, [FromBody] ImportPayload payload)
        {
            var userId = CurrentUserId();
            if (userId is null)
            {
                return this.CreateProblem(
                    StatusCodes.Status400BadRequest,
                    title: "Missing required header",
                    detail: "The X-User-Id header is required.");
            }

            mode = string.IsNullOrWhiteSpace(mode) ? "merge" : mode.ToLowerInvariant();

            if (payload is null)
            {
                return this.CreateProblem(
                    StatusCodes.Status400BadRequest,
                    title: "Invalid payload",
                    detail: "A request body is required for import.");
            }

            if (payload.Version != 1)
            {
                return this.CreateValidationProblem("version", "Unsupported version.");
            }

            var allIds = payload.Collection.Select(c => c.CardPrintingId)
                .Concat(payload.Wishlist.Select(w => w.CardPrintingId))
                .Concat(payload.Decks.SelectMany(d => d.Cards.Select(c => c.CardPrintingId)))
                .ToHashSet();

            List<int> missing = new();
            if (allIds.Count > 0)
            {
                var present = await _db.CardPrintings
                    .Where(cp => allIds.Contains(cp.Id))
                    .Select(cp => cp.Id)
                    .ToListAsync();
                missing = allIds.Except(present).ToList();
            }

            if (missing.Any())
            {
                return this.CreateValidationProblem(new Dictionary<string, string[]>
                {
                    ["cardPrintingId"] = new[]
                    {
                        $"Unknown CardPrintingId(s): {string.Join(", ", missing)}"
                    }
                });
            }

            using var trx = await _db.Database.BeginTransactionAsync();

            var replaceMode = mode == "replace";
            if (replaceMode)
            {
                var decks = await _db.Decks.Where(d => d.UserId == userId).ToListAsync();
                if (decks.Count > 0) _db.Decks.RemoveRange(decks);

                var userCards = await _db.UserCards.Where(uc => uc.UserId == userId).ToListAsync();
                if (userCards.Count > 0) _db.UserCards.RemoveRange(userCards);

                await _db.SaveChangesAsync();
            }
            else if (mode != "merge")
            {
                return this.CreateValidationProblem("mode", "Mode must be merge or replace.");
            }

            var userCardMap = await _db.UserCards
                .Where(uc => uc.UserId == userId)
                .ToDictionaryAsync(uc => uc.CardPrintingId);

            foreach (var c in payload.Collection)
            {
                if (!userCardMap.TryGetValue(c.CardPrintingId, out var row))
                {
                    row = new UserCard
                    {
                        UserId = userId.Value,
                        CardPrintingId = c.CardPrintingId
                    };
                    userCardMap[c.CardPrintingId] = row;
                    _db.UserCards.Add(row);
                }

                var owned = Math.Max(0, c.QtyOwned);
                var proxy = Math.Max(0, c.QtyProxyOwned);

                if (replaceMode)
                {
                    row.QuantityOwned = owned;
                    row.QuantityProxyOwned = proxy;
                }
                else
                {
                    row.QuantityOwned = Math.Max(0, row.QuantityOwned + owned);
                    row.QuantityProxyOwned = Math.Max(0, row.QuantityProxyOwned + proxy);
                }
            }

            foreach (var w in payload.Wishlist)
            {
                if (!userCardMap.TryGetValue(w.CardPrintingId, out var row))
                {
                    row = new UserCard
                    {
                        UserId = userId.Value,
                        CardPrintingId = w.CardPrintingId
                    };
                    userCardMap[w.CardPrintingId] = row;
                    _db.UserCards.Add(row);
                }

                var wanted = Math.Max(0, w.Qty);
                if (replaceMode)
                {
                    row.QuantityWanted = wanted;
                }
                else
                {
                    row.QuantityWanted = Math.Max(row.QuantityWanted, wanted);
                }
            }

            var decksByKey = await _db.Decks
                .Where(d => d.UserId == userId)
                .Include(d => d.Cards)
                .ToDictionaryAsync(d => (d.Game, d.Name));

            foreach (var d in payload.Decks)
            {
                if (!decksByKey.TryGetValue((d.Game, d.Name), out var deck))
                {
                    deck = new Deck
                    {
                        UserId = userId.Value,
                        Game = d.Game,
                        Name = d.Name,
                        Description = d.Description
                    };
                    decksByKey[(d.Game, d.Name)] = deck;
                    _db.Decks.Add(deck);
                }
                else if (replaceMode || d.Description != deck.Description)
                {
                    deck.Description = d.Description;
                }

                var existingCards = deck.Cards.ToDictionary(c => c.CardPrintingId);

                foreach (var card in d.Cards)
                {
                    if (!existingCards.TryGetValue(card.CardPrintingId, out var deckCard))
                    {
                        deckCard = new DeckCard
                        {
                            Deck = deck,
                            CardPrintingId = card.CardPrintingId
                        };
                        existingCards[card.CardPrintingId] = deckCard;
                        deck.Cards.Add(deckCard);
                        _db.DeckCards.Add(deckCard);
                    }

                    var inDeck = Math.Max(0, card.InDeck);
                    var idea = Math.Max(0, card.Idea);
                    var acquire = Math.Max(0, card.Acquire);
                    var proxy = Math.Max(0, card.Proxy);

                    if (replaceMode)
                    {
                        deckCard.QuantityInDeck = inDeck;
                        deckCard.QuantityIdea = idea;
                        deckCard.QuantityAcquire = acquire;
                        deckCard.QuantityProxy = proxy;
                    }
                    else
                    {
                        deckCard.QuantityInDeck = Math.Max(0, deckCard.QuantityInDeck + inDeck);
                        deckCard.QuantityIdea = Math.Max(0, deckCard.QuantityIdea + idea);
                        deckCard.QuantityAcquire = Math.Max(0, deckCard.QuantityAcquire + acquire);
                        deckCard.QuantityProxy = Math.Max(0, deckCard.QuantityProxy + proxy);
                    }
                }
            }

            await _db.SaveChangesAsync();
            await trx.CommitAsync();

            return Ok(new
            {
                imported = new
                {
                    collection = payload.Collection.Count,
                    wishlist = payload.Wishlist.Count,
                    decks = payload.Decks.Count,
                    deckCards = payload.Decks.SelectMany(d => d.Cards).Count()
                }
            });
        }
    }
}
