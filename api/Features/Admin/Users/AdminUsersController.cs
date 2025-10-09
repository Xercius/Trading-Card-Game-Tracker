using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using api.Common.Errors;
using api.Data;
using api.Filters;
using api.Models;

namespace api.Features.Admin.Users;

[ApiController]
[Route("api/admin/users")]
[Authorize]
[RequireAdmin]
public sealed class AdminUsersController : ControllerBase
{
    private readonly AppDbContext _db;

    public AdminUsersController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AdminUserResponse>>> GetUsers()
    {
        var users = await _db.Users
            .OrderBy(u => u.Username)
            .Select(u => Map(u))
            .ToListAsync();

        return Ok(users);
    }

    [HttpPost]
    public async Task<ActionResult<AdminUserResponse>> CreateUser([FromBody] AdminCreateUserRequest request)
    {
        if (request is null)
        {
            return this.CreateValidationProblem(
                new Dictionary<string, string[]>
                {
                    ["request"] = new[] { "A request body is required." }
                },
                title: "Invalid payload");
        }

        var trimmedName = request.Name?.Trim();
        if (string.IsNullOrWhiteSpace(trimmedName))
        {
            return this.CreateValidationProblem(
                "name",
                "A non-empty name is required to create a user.");
        }

        var name = trimmedName!;
        var user = new User
        {
            Username = name,
            DisplayName = name,
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        var response = Map(user);
        return Created($"/api/admin/users/{user.Id}", response);
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<AdminUserResponse>> UpdateUser(int id, [FromBody] AdminUpdateUserRequest request)
    {
        if (request is null)
        {
            return this.CreateValidationProblem(
                new Dictionary<string, string[]>
                {
                    ["request"] = new[] { "A request body is required." }
                },
                title: "Invalid payload");
        }

        await using var tx = await _db.Database.BeginTransactionAsync();

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == id);
        if (user is null)
        {
            await tx.RollbackAsync();
            return this.CreateProblem(
                StatusCodes.Status404NotFound,
                detail: $"User {id} was not found.");
        }

        if (request.Name is not null)
        {
            var trimmedName = request.Name.Trim();
            if (string.IsNullOrWhiteSpace(trimmedName))
            {
                await tx.RollbackAsync();
                return this.CreateValidationProblem(
                    "name",
                    "Name cannot be blank.");
            }

            user.Username = trimmedName;
            user.DisplayName = trimmedName;
        }

        if (request.IsAdmin.HasValue && request.IsAdmin.Value != user.IsAdmin)
        {
            if (!request.IsAdmin.Value)
            {
                var guardResult = await this.EnsureAnotherAdminRemainsAsync(_db, user.IsAdmin);
                if (guardResult is { } actionResult)
                {
                    await tx.RollbackAsync();
                    return actionResult;
                }
            }

            user.IsAdmin = request.IsAdmin.Value;
        }

        await _db.SaveChangesAsync();
        await tx.CommitAsync();

        return Ok(Map(user));
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteUser(int id)
    {
        await using var tx = await _db.Database.BeginTransactionAsync();

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == id);
        if (user is null)
        {
            await tx.RollbackAsync();
            return this.CreateProblem(
                StatusCodes.Status404NotFound,
                detail: $"User {id} was not found.");
        }

        var guardResult = await this.EnsureAnotherAdminRemainsAsync(_db, user.IsAdmin);
        if (guardResult is { } actionResult)
        {
            await tx.RollbackAsync();
            return actionResult;
        }

        _db.Users.Remove(user);
        await _db.SaveChangesAsync();
        await tx.CommitAsync();

        return NoContent();
    }

    private static AdminUserResponse Map(User user)
    {
        var display = string.IsNullOrWhiteSpace(user.DisplayName) ? user.Username : user.DisplayName;
        return new AdminUserResponse(
            user.Id,
            display,
            user.Username,
            user.DisplayName,
            user.IsAdmin,
            user.CreatedUtc);
    }
}
