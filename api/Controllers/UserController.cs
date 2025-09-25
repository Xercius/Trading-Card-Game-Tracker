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
    [RequireUserHeader]
    // Legacy, userId-in-route endpoints kept for compatibility:
    [Route("api/user/{userId:int}")]
    public class UserController : ControllerBase
    {
        private readonly AppDbContext _db;
        public UserController(AppDbContext db) => _db = db;

        // -----------------------------
        // Helpers
        // -----------------------------

        private bool TryResolveCurrentUserId(out int userId, out IActionResult? error)
        {
            var me = HttpContext.GetCurrentUser();
            if (me is null) { error = StatusCode(403, "User missing."); userId = 0; return false; }
            error = null; userId = me.Id; return true;
        }

        private bool UserMismatch(int userId)
        {
            var me = HttpContext.GetCurrentUser();
            return me is null || (!me.IsAdmin && me.Id != userId);
        }

        private bool NotAdmin()
        {
            var me = HttpContext.GetCurrentUser();
            return me is null || !me.IsAdmin;
        }

        // -----------------------------
        // Core (single source of truth)
        // -----------------------------

        private async Task<IActionResult> GetUserCore(int userId)
        {
            var u = await _db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == userId);
            if (u is null) return NotFound();
            return Ok(new UserDto(u.Id, u.Username, u.DisplayName, u.IsAdmin));
        }

        private async Task<IActionResult> PatchUserCore(int userId, JsonElement updates)
        {
            var u = await _db.Users.FirstOrDefaultAsync(x => x.Id == userId);
            if (u is null) return NotFound();

            if (updates.TryGetProperty("userName", out var n) && n.ValueKind == JsonValueKind.String)
                u.Username = n.GetString()!.Trim();

            if (updates.TryGetProperty("displayName", out var d) && d.ValueKind == JsonValueKind.String)
                u.DisplayName = d.GetString()!.Trim();

            // IsAdmin changes are admin-only; handled via dedicated admin endpoints.

            await _db.SaveChangesAsync();
            return NoContent();
        }

        private async Task<IActionResult> PutUserCore(int userId, UpdateUserDto dto)
        {
            if (dto is null) return BadRequest();

            var u = await _db.Users.FirstOrDefaultAsync(x => x.Id == userId);
            if (u is null) return NotFound();

            u.Username = dto.Username?.Trim() ?? u.Username;
            u.DisplayName = dto.DisplayName?.Trim() ?? u.DisplayName;

            await _db.SaveChangesAsync();
            return NoContent();
        }

        // Admin-only: list users
        private async Task<IActionResult> ListUsersCore(string? name, string? displayName, bool? isAdmin)
        {
            if (NotAdmin()) return StatusCode(403, "Admin required.");

            var q = _db.Users.AsQueryable();

            if (!string.IsNullOrWhiteSpace(name))
            {
                var n = name.Trim();
                q = q.Where(u => u.Username!.Contains(n));
            }
            if (!string.IsNullOrWhiteSpace(displayName))
            {
                var d = displayName.Trim();
                q = q.Where(u => u.DisplayName!.Contains(d));
            }
            if (isAdmin.HasValue) q = q.Where(u => u.IsAdmin == isAdmin.Value);

            var rows = await q
                .OrderBy(u => u.Username)
                .Select(u => new UserDto(u.Id, u.Username, u.DisplayName, u.IsAdmin))
                .ToListAsync();

            return Ok(rows);
        }

        // Admin-only: set IsAdmin
        private async Task<IActionResult> SetAdminCore(int userId, bool isAdmin)
        {
            if (NotAdmin()) return StatusCode(403, "Admin required.");

            var u = await _db.Users.FirstOrDefaultAsync(x => x.Id == userId);
            if (u is null) return NotFound();

            u.IsAdmin = isAdmin;
            await _db.SaveChangesAsync();
            return NoContent();
        }

        // Admin-only: delete user
        private async Task<IActionResult> DeleteUserCore(int userId)
        {
            if (NotAdmin()) return StatusCode(403, "Admin required.");

            var u = await _db.Users.FirstOrDefaultAsync(x => x.Id == userId);
            if (u is null) return NotFound();

            // Optional: cascade checks (collections, decks, etc.) if not configured via FK cascade.
            _db.Users.Remove(u);
            await _db.SaveChangesAsync();
            return NoContent();
        }

        // -----------------------------
        // Legacy routes (userId in URL)
        // -----------------------------

        // GET /api/user/{userId}
        [HttpGet]
        public async Task<IActionResult> GetUser(int userId)
        {
            if (UserMismatch(userId)) return StatusCode(403, "User mismatch.");
            return await GetUserCore(userId);
        }

        // PATCH /api/user/{userId}
        [HttpPatch]
        public async Task<IActionResult> PatchUser(int userId, [FromBody] JsonElement updates)
        {
            if (UserMismatch(userId)) return StatusCode(403, "User mismatch.");
            return await PatchUserCore(userId, updates);
        }

        // PUT /api/user/{userId}
        [HttpPut]
        public async Task<IActionResult> PutUser(int userId, [FromBody] UpdateUserDto dto)
        {
            if (UserMismatch(userId)) return StatusCode(403, "User mismatch.");
            return await PutUserCore(userId, dto);
        }

        // Admin: DELETE /api/user/{userId}
        [HttpDelete]
        public async Task<IActionResult> DeleteUser(int userId) => await DeleteUserCore(userId);

        // Admin: PUT /api/user/{userId}/admin?value=true|false
        [HttpPut("admin")]
        public async Task<IActionResult> SetAdmin(int userId, [FromQuery] bool value = true)
            => await SetAdminCore(userId, value);

        // -----------------------------
        // Auth-derived aliases (preferred)
        // -----------------------------

        // GET /api/user/me
        [HttpGet("/api/user/me")]
        public async Task<IActionResult> GetMe()
        {
            if (!TryResolveCurrentUserId(out var uid, out var err)) return err!;
            return await GetUserCore(uid);
        }

        // PATCH /api/user/me
        [HttpPatch("/api/user/me")]
        public async Task<IActionResult> PatchMe([FromBody] JsonElement updates)
        {
            if (!TryResolveCurrentUserId(out var uid, out var err)) return err!;
            return await PatchUserCore(uid, updates);
        }

        // PUT /api/user/me
        [HttpPut("/api/user/me")]
        public async Task<IActionResult> PutMe([FromBody] UpdateUserDto dto)
        {
            if (!TryResolveCurrentUserId(out var uid, out var err)) return err!;
            return await PutUserCore(uid, dto);
        }

        // Admin: GET /api/users
        [HttpGet("/api/users")]
        public async Task<IActionResult> ListUsers([FromQuery] string? name = null, [FromQuery] string? displayName = null, [FromQuery] bool? isAdmin = null)
            => await ListUsersCore(name, displayName, isAdmin);

        // Admin: PUT /api/users/{userId}/admin?value=true|false
        [HttpPut("/api/users/{targetUserId:int}/admin")]
        public async Task<IActionResult> SetAdminById(int targetUserId, [FromQuery] bool value = true)
            => await SetAdminCore(targetUserId, value);

        // Admin: DELETE /api/users/{userId}
        [HttpDelete("/api/users/{targetUserId:int}")]
        public async Task<IActionResult> DeleteUserById(int targetUserId)
            => await DeleteUserCore(targetUserId);
    }
}

