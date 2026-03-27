using api.Common.Errors;
using api.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace api.Features.Users;

[ApiController]
[Route("api/user")]
public class UsersController : ControllerBase
{
    private readonly AppDbContext _db;

    public UsersController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetUser(int id, CancellationToken cancellationToken)
    {
        var user = await _db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == id, cancellationToken);

        if (user is null)
        {
            return this.CreateProblem(
                StatusCodes.Status404NotFound,
                detail: $"User {id} was not found.");
        }

        return Ok(new { user.Id, user.Username, user.DisplayName, user.IsAdmin, user.CreatedUtc });
    }
}
