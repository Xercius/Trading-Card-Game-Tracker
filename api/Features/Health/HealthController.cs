using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace api.Features.Health;

[ApiController]
[Route("api/health")]
public sealed class HealthController : ControllerBase
{
    [HttpGet]
    [AllowAnonymous]
    public ActionResult<object> Get()
    {
        return Ok(new { status = "healthy", timestamp = DateTimeOffset.UtcNow });
    }
}
