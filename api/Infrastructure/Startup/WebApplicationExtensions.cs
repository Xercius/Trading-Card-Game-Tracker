using Microsoft.Extensions.Options;

namespace api.Infrastructure.Startup;

internal static class WebApplicationExtensions
{
    public static WebApplication UseApiPipeline(this WebApplication app)
    {
        app.UseForwardedHeaders();
        app.UseExceptionHandler();

        if (!app.Environment.IsEnvironment("Testing"))
        {
            app.UseHttpsRedirection();
        }

        app.UseStaticFiles();
        app.UseRouting();
        app.UseRateLimiter();
        var corsOptions = app.Services.GetRequiredService<IOptions<CorsPolicyOptions>>().Value;
        app.UseCors(string.IsNullOrWhiteSpace(corsOptions.PolicyName) ? "AllowReact" : corsOptions.PolicyName);
        app.UseAuthentication();
        app.UseAuthorization();

        return app;
    }

    public static WebApplication UseDeveloperApi(this WebApplication app)
    {
        // Enable developer exception page for detailed error information
        app.UseDeveloperExceptionPage();
        return app;
    }
}
