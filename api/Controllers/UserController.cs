using System.Linq;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using api.Data;
using api.Models;

public record UserDto(int Id, string Username, string Role);
public record CreateUserDto(string Username, string Role);
public record UpdateUserDto(string Username, string Role);

namespace api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class UserController : ControllerBase
    {
        private readonly AppDbContext _db;
        public UserController(AppDbContext db) => _db = db;

        [HttpGet]
        public async Task<ActionResult<IEnumerable<UserDto>>> GetAll()
            => Ok(await _db.Users.Select(u => new UserDto(u.Id, u.Username, u.Role)).ToListAsync());

        [HttpGet("{id:int}")]
        public async Task<ActionResult<UserDto>> GetOne(int id)
        {
            var u = await _db.Users.FindAsync(id);
            return u is null ? NotFound() : Ok(new UserDto(u.Id, u.Username, u.Role));
        }

        [HttpPost]
        public async Task<ActionResult<UserDto>> Create([FromBody] CreateUserDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Username)) return BadRequest("Username required.");
            if (await _db.Users.AnyAsync(x => x.Username == dto.Username)) return Conflict("Username exists.");

            var u = new User { Username = dto.Username, Role = string.IsNullOrWhiteSpace(dto.Role) ? "User" : dto.Role };
            _db.Users.Add(u);
            await _db.SaveChangesAsync();
            return CreatedAtAction(nameof(GetOne), new { id = u.Id }, new UserDto(u.Id, u.Username, u.Role));
        }

        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(int id, [FromBody] UpdateUserDto dto)
        {
            var u = await _db.Users.FirstOrDefaultAsync(x => x.Id == id);
            if (u is null) return NotFound();
            if (string.IsNullOrWhiteSpace(dto.Username)) return BadRequest("Username required.");
            if (await _db.Users.AnyAsync(x => x.Username == dto.Username && x.Id != id)) return Conflict("Username exists.");

            u.Username = dto.Username;
            u.Role = string.IsNullOrWhiteSpace(dto.Role) ? u.Role : dto.Role;
            await _db.SaveChangesAsync();
            return NoContent();
        }

        [HttpPatch("{id:int}")]
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
            if (updates.TryGetProperty("role", out var r) && r.ValueKind == JsonValueKind.String)
                u.Role = r.GetString()!;

            await _db.SaveChangesAsync();
            return NoContent();
        }

        [HttpDelete("{id:int}")]
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
