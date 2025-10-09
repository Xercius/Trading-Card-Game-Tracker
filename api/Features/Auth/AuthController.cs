using System;
using System.Linq;
using System.Threading.Tasks;
using api.Authentication;
using api.Data;
using api.Features.Auth;
using api.Features.Users.Dtos;
using api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;

namespace api.Features.Auth;

[ApiController]
[Route("api/auth")]
public sealed class AuthController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IPasswordHasher<User> _passwordHasher;
    private readonly IJwtTokenService _tokenService;
    private readonly IHostEnvironment _environment;

    public AuthController(AppDbContext db, IPasswordHasher<User> passwordHasher, IJwtTokenService tokenService, IHostEnvironment environment)
    {
        _db = db;
        _passwordHasher = passwordHasher;
        _tokenService = tokenService;
        _environment = environment;
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<LoginResponse>> Login([FromBody] LoginRequest request)
    {
        if (request is null)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Invalid payload",
                Detail = "A request body is required.",
                Status = StatusCodes.Status400BadRequest
            });
        }

        var username = request.Username?.Trim();
        var password = request.Password ?? string.Empty;

        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            return Unauthorized();
        }

        var normalized = username.ToLowerInvariant();

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Username.ToLower() == normalized);
        if (user is null)
        {
            await Task.Delay(Random.Shared.Next(25, 75));
            return Unauthorized();
        }

        if (user.PasswordHash is null)
        {
            return Unauthorized();
        }

        var verification = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, password);
        if (verification == PasswordVerificationResult.Failed)
        {
            return Unauthorized();
        }

        return CreateLoginResult(user);
    }

    [HttpPost("impersonate")]
    [AllowAnonymous]
    public async Task<ActionResult<LoginResponse>> Impersonate([FromBody] ImpersonateRequest request)
    {
        if (!_environment.IsDevelopment() && !_environment.IsEnvironment("Testing"))
        {
            return NotFound();
        }

        if (request is null || request.UserId <= 0)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Invalid payload",
                Detail = "A valid userId is required.",
                Status = StatusCodes.Status400BadRequest
            });
        }

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == request.UserId);
        if (user is null)
        {
            return Unauthorized();
        }

        return CreateLoginResult(user);
    }

    private ActionResult<LoginResponse> CreateLoginResult(User user)
    {
        var username = user.Username?.Trim();
        var displayName = user.DisplayName?.Trim();

        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(displayName))
        {
            return Problem(
                statusCode: StatusCodes.Status500InternalServerError,
                title: "User record invalid",
                detail: "User record invalid: missing Username or DisplayName");
        }

        var token = _tokenService.CreateToken(new User
        {
            Id = user.Id,
            Username = username,
            DisplayName = displayName,
            IsAdmin = user.IsAdmin
        });

        var userDto = new UserResponse(user.Id, username, displayName, user.IsAdmin);
        return Ok(new LoginResponse(token.AccessToken, token.ExpiresAtUtc, userDto));
    }
}
