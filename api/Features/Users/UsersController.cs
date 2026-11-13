using api.Common.Errors;
using api.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace api.Features.Users;

/// <summary>
/// Provides read-only access to application user profiles.
/// </summary>
[ApiController]
[Route("api/users")]
[Route("api/user")]
public sealed class UsersController(AppDbContext db) : ControllerBase
{
    /// <summary>Gets a user profile by ID.</summary>
    [HttpGet("{id:int}")]
    public async Task<ActionResult<UserResponse>> GetUser(int id, CancellationToken cancellationToken)
    {
        var user = await db.Users
            .AsNoTracking()
            .Where(u => u.Id == id)
            .Select(u => new UserResponse(u.Id, u.Username, u.DisplayName, u.IsAdmin, u.CreatedUtc))
            .FirstOrDefaultAsync(cancellationToken);

        if (user is null)
        {
            return this.CreateProblem(
                StatusCodes.Status404NotFound,
                detail: $"User {id} was not found.");
        }

        return Ok(user);
    }
}

/// <summary>Public user profile DTO.</summary>
public sealed record UserResponse(
    int Id,
    string Username,
    string DisplayName,
    bool IsAdmin,
    DateTimeOffset CreatedUtc);
