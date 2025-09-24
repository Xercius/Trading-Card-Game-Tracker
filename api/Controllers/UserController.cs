using api.Data;
using api.Filters;
using api.Middleware;
using api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Text.Json;

public record UserDto(
    int Id,
    string Username,
    string DisplayName,
    bool IsAdmin
);
public record CreateUserDto(
    string Username,
    string DisplayName,
    bool IsAdmin
);
public record UpdateUserDto(
    string Username,
    string DisplayName,
    bool IsAdmin
);

namespace api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class UserController : ControllerBase
    {
        private readonly AppDbContext _db;
        public UserController(AppDbContext db) => _db = db;

        [HttpGet]
        [AdminGuard]
        public async Task<ActionResult<IEnumerable<UserDto>>> GetAll()
            => Ok(await _db.Users.Select(u => new UserDto(u.Id, u.Username, u.DisplayName, u.IsAdmin)).ToListAsync());

        [HttpGet("{id:int}")]
        [RequireUserHeader]
        public async Task<ActionResult<UserDto>> GetOne(int id)
        {
            var u = await _db.Users.FindAsync(id);
            return u is null ? NotFound() : Ok(new UserDto(u.Id, u.Username, u.DisplayName, u.IsAdmin));
        }

        [HttpPost]
        [AdminGuard]
        public async Task<ActionResult<UserDto>> Create([FromBody] CreateUserDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Username)) return BadRequest("Username required.");
            if (await _db.Users.AnyAsync(x => x.Username == dto.Username)) return Conflict("Username exists.");

            var u = new User { Username = dto.Username, DisplayName = dto.DisplayName, IsAdmin = dto.IsAdmin };
            _db.Users.Add(u);
            await _db.SaveChangesAsync();
            return CreatedAtAction(nameof(GetOne), new { id = u.Id }, new UserDto(u.Id, u.Username, u.DisplayName, u.IsAdmin));
        }

        [HttpPut("{id:int}")]
        [AdminGuard]
        public async Task<IActionResult> Update(int id, [FromBody] UpdateUserDto dto)
        {
            var u = await _db.Users.FirstOrDefaultAsync(x => x.Id == id);
            if (u is null) return NotFound();
            if (string.IsNullOrWhiteSpace(dto.Username)) return BadRequest("Username required.");
            if (await _db.Users.AnyAsync(x => x.Username == dto.Username && x.Id != id)) return Conflict("Username exists.");

            u.Username = dto.Username;
            u.DisplayName = dto.DisplayName;
            u.IsAdmin = dto.IsAdmin;
            await _db.SaveChangesAsync();
            return NoContent();
        }

        [HttpPatch("{id:int}")]
        [AdminGuard]
        public async Task<IActionResult> Patch(int id, [FromBody] JsonElement updates)
        {
            var u = await _db.Users.FirstOrDefaultAsync(x => x.Id == id);
            if (u is null) return NotFound();

            if (updates.TryGetProperty("username", out var un) && un.ValueKind == JsonValueKind.String)
            {
                var name = un.GetString()!;
                if (string.IsNullOrWhiteSpace(name)) return BadRequest("Username required.");
                if (await _db.Users.AnyAsync(x => x.Username == name && x.Id != id)) return Conflict("Username exists.");
                u.Username = name;
            }
            if (updates.TryGetProperty("displayName", out var dn) && dn.ValueKind == JsonValueKind.String)
            {
                var v = dn.GetString()!;
                if (string.IsNullOrWhiteSpace(v)) return BadRequest("DisplayName required.");
                u.DisplayName = v;
            }
            if (updates.TryGetProperty("isAdmin", out var ia) ||
                updates.TryGetProperty("IsAdmin", out ia))
            {
                if (ia.ValueKind == JsonValueKind.True || ia.ValueKind == JsonValueKind.False)
                    u.IsAdmin = ia.GetBoolean();
                else if (ia.ValueKind == JsonValueKind.Number && ia.TryGetInt32(out var n))
                    u.IsAdmin = n != 0;
                else
                    return BadRequest("isAdmin must be boolean.");
            }

        await _db.SaveChangesAsync();
            return NoContent();
        }

        [HttpDelete("{id:int}")]
        [AdminGuard]
        public async Task<IActionResult> Delete(int id)
        {
            var u = await _db.Users.FirstOrDefaultAsync(x => x.Id == id);
            if (u is null) return NotFound();
            _db.Users.Remove(u);
            await _db.SaveChangesAsync();
            return NoContent();
        }
    }
}
