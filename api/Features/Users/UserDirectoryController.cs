using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using api.Data;
using api.Features.Users.Dtos;
using api.Filters;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace api.Features.Users;

[ApiController]
[RequireUserHeader]
[Route("api/user/list")]
public sealed class UserDirectoryController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<UserResponse>>> ListUsers(CancellationToken ct = default)
    {
        var users = await db.Users
            .AsNoTracking()
            .OrderBy(u => u.Username)
            .Select(u => new UserResponse(u.Id, u.Username ?? string.Empty, u.DisplayName ?? string.Empty, u.IsAdmin))
            .ToListAsync(ct);

        return Ok(users);
    }
}
