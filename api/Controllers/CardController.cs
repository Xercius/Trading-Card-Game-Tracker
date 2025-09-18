using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using api.Data;
using api.Models;

namespace api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class CardController : ControllerBase
    {
        private readonly AppDbContext _context;

        public CardController(AppDbContext context)
        {
            _context = context;
        }

        // GET: api/card
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Card>>> GetCards()
        {
            return await _context.Cards
                .Include(c => c.Printings)
                .ToListAsync();
        }

        // GET: api/card/{id}
        [HttpGet("{id}")]
        public async Task<ActionResult<Card>> GetCard(int id)
        {
            var card = await _context.Cards
                .Include(c => c.Printings)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (card == null)
            {
                return NotFound();
            }

            return card;
        }

        // POST: api/card
        [HttpPost]
        public async Task<ActionResult<Card>> AddCard(Card card)
        {
            // Attach printings to card explicitly if any exist
            if (card.Printings != null)
            {
                foreach (var printing in card.Printings)
                {
                    printing.Card = card;
                }
            }

            _context.Cards.Add(card);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetCard), new { id = card.Id }, card);
        }
    }
}
