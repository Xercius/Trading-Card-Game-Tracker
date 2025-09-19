using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using api.Data;
using api.Models;

// Read DTOs
public record CardPrintingDto(int Id, string Set, string Number, string Rarity, string Style, string? ImageUrl);
public record CardDto(int Id, string Game, string Name, string CardType, string? Description, List<CardPrintingDto> Printings);

// Write DTOs (for POST)
public record CreateCardPrintingDto(string Set, string Number, string Rarity, string Style, string? ImageUrl);
public record CreateCardDto(string Game, string Name, string CardType, string? Description, List<CreateCardPrintingDto> Printings);

namespace api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class CardController : ControllerBase
    {
        private readonly AppDbContext _db;
        public CardController(AppDbContext db) => _db = db;

        // GET: /api/card
        [HttpGet]
        public async Task<ActionResult<IEnumerable<CardDto>>> GetAll()
        {
            var data = await _db.Cards
                .Select(c => new CardDto(
                    c.Id, c.Game, c.Name, c.CardType, c.Description,
                    c.Printings.Select(p => new CardPrintingDto(
                        p.Id, p.Set, p.Number, p.Rarity, p.Style, p.ImageUrl
                    )).ToList()
                ))
                .ToListAsync();

            return Ok(data);
        }

        // GET: /api/card/{id}
        [HttpGet("{id:int}")]
        public async Task<ActionResult<CardDto>> GetOne(int id)
        {
            var c = await _db.Cards
                .Where(x => x.Id == id)
                .Select(x => new CardDto(
                    x.Id, x.Game, x.Name, x.CardType, x.Description,
                    x.Printings.Select(p => new CardPrintingDto(
                        p.Id, p.Set, p.Number, p.Rarity, p.Style, p.ImageUrl
                    )).ToList()
                ))
                .FirstOrDefaultAsync();

            if (c is null) return NotFound();
            return Ok(c);
        }

        // POST: /api/card
        [HttpPost]
        public async Task<ActionResult<CardDto>> Create([FromBody] CreateCardDto dto)
        {
            if (dto is null) return BadRequest();

            var card = new Card
            {
                Game = dto.Game,
                Name = dto.Name,
                CardType = dto.CardType,
                Description = dto.Description,
                Printings = (dto.Printings ?? new List<CreateCardPrintingDto>())
                    .Select(p => new CardPrinting
                    {
                        Set = p.Set,
                        Number = p.Number,
                        Rarity = p.Rarity,
                        Style = p.Style,
                        ImageUrl = p.ImageUrl
                    }).ToList()
            };

            _db.Cards.Add(card);
            await _db.SaveChangesAsync();

            var result = new CardDto(
                card.Id, card.Game, card.Name, card.CardType, card.Description,
                card.Printings.Select(p => new CardPrintingDto(
                    p.Id, p.Set, p.Number, p.Rarity, p.Style, p.ImageUrl
                )).ToList()
            );

            return CreatedAtAction(nameof(GetOne), new { id = card.Id }, result);
        }
    }
}
