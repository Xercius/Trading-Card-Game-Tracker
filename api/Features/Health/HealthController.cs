using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace api.Features.Health;

/// <summary>
/// Health check endpoint.
/// </summary>
[ApiController]
[Route("api/health")]
public sealed class HealthController(TimeProvider timeProvider) : ControllerBase
{
    /// <summary>Returns the current service health status and UTC timestamp.</summary>
    [HttpGet]
    [AllowAnonymous]
    public ActionResult<HealthResponse> Get()
    {
        return Ok(new HealthResponse("healthy", timeProvider.GetUtcNow()));
    }
}

/// <summary>Response payload for <see cref="HealthController.Get"/>.</summary>
public sealed record HealthResponse(string Status, DateTimeOffset Timestamp);
