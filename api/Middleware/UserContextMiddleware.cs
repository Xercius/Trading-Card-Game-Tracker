using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using api.Data;

namespace api.Middleware
{
    public record CurrentUser(int Id, string Username, bool IsAdmin);

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, Inherited = true)]
    public sealed class RequireUserHeaderAttribute : Attribute { }

    public static class HttpContextUserExtensions
    {
        public static CurrentUser? GetCurrentUser(this HttpContext ctx)
        => ctx.Items.TryGetValue("User", out var v) && v is CurrentUser cu ? cu : null;
    }

    public class UserContextMiddleware
    {
        private readonly RequestDelegate _next;
        public UserContextMiddleware(RequestDelegate next) => _next = next;

        public async Task Invoke(HttpContext ctx, AppDbContext db)
        {
            CurrentUser? cu = null;

            if (ctx.Request.Headers.TryGetValue("X-User-Id", out var raw) && int.TryParse(raw, out var id))
            {
                var u = await db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
                if (u != null)
                    cu = new CurrentUser(u.Id, u.Username, u.IsAdmin);
            }

            // attach if found
            if (cu != null) ctx.Items["User"] = cu;

            // enforce header for marked endpoints
            var ep = ctx.GetEndpoint();
            var requires = ep?.Metadata.GetMetadata<RequireUserHeaderAttribute>() != null;
            if (requires && cu == null)
            {
                var factory = ctx.RequestServices.GetRequiredService<ProblemDetailsFactory>();
                var problem = factory.CreateProblemDetails(
                    ctx,
                    statusCode: StatusCodes.Status400BadRequest,
                    title: "Missing required header",
                    detail: "The X-User-Id header is required or invalid.");

                ctx.Response.StatusCode = StatusCodes.Status400BadRequest;
                ctx.Response.ContentType = "application/problem+json";
                await ctx.Response.WriteAsJsonAsync(problem);
                return;
            }

            await _next(ctx);
        }
    }
}
