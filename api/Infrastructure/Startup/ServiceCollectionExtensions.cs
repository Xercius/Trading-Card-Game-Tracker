using api.Common.Errors;
using api.Data;
using api.Features.Decks.Services;
using api.Features.Admin.Sync.Services;
using api.Importing;
using api.Importing.Pokemon;
using api.Importing.Swccg;
using api.Infrastructure.Auth;
using api.Models;
using api.Shared.Importing;
using FluentValidation;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;

namespace api.Infrastructure.Startup;

internal static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApiBasics(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddProblemDetailsAndErrors();
        services.AddControllers(options => options.Filters.Add<AdminSyncAntiforgeryFilter>())
            .AddJsonOptions(JsonOptionsConfigurator.Configure);
        services.AddAdminAuthAndAntiforgery();
        services.AddApiBehaviorAndMapping();
        services.AddCorsConfiguration(configuration);
        services.AddForwardedHeadersConfiguration(configuration);
        services.AddHttpsRedirection(options => options.HttpsPort = 7226);

        return services;
    }

    private static void AddProblemDetailsAndErrors(this IServiceCollection services)
    {
        services.AddSingleton<ProblemDetailsFactory, api.Common.Errors.DefaultProblemDetailsFactory>();
        services.AddExceptionHandler<ApiExceptionHandler>();

        services.AddProblemDetails(options =>
        {
            options.CustomizeProblemDetails = context =>
            {
                var httpContext = context.HttpContext;
                var problemDetails = context.ProblemDetails;
                var statusCode = problemDetails.Status ?? StatusCodes.Status500InternalServerError;

                if (ProblemTypes.TryGet(statusCode, out var problemType))
                {
                    problemType.Apply(httpContext, problemDetails);
                }
                else if (string.IsNullOrEmpty(problemDetails.Instance))
                {
                    problemDetails.Instance = httpContext.Request.Path;
                }

                var traceId = Activity.Current?.Id ?? httpContext.TraceIdentifier;
                if (!string.IsNullOrEmpty(traceId))
                {
                    problemDetails.Extensions["traceId"] = traceId;
                }
            };
        });
    }

    private static void AddAdminAuthAndAntiforgery(this IServiceCollection services)
    {
        services.AddAuthentication(AdminTokenAuthenticationHandler.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, AdminTokenAuthenticationHandler>(
                AdminTokenAuthenticationHandler.SchemeName,
                _ => { });

        services.AddSingleton<IPasswordHasher<User>, PasswordHasher<User>>();
        services.AddScoped<AdminAccountProvisioner>();
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
        });
        services.AddAuthorization();
    }

    private static void AddApiBehaviorAndMapping(this IServiceCollection services)
    {
        services.Configure<ApiBehaviorOptions>(options =>
        {
            options.ClientErrorMapping[StatusCodes.Status400BadRequest] = new ClientErrorData { Link = ProblemTypes.BadRequest.Type, Title = ProblemTypes.BadRequest.Title };
            options.ClientErrorMapping[StatusCodes.Status404NotFound] = new ClientErrorData { Link = ProblemTypes.NotFound.Type, Title = ProblemTypes.NotFound.Title };
            options.ClientErrorMapping[StatusCodes.Status409Conflict] = new ClientErrorData { Link = ProblemTypes.Conflict.Type, Title = ProblemTypes.Conflict.Title };

            options.InvalidModelStateResponseFactory = context =>
            {
                var factory = context.HttpContext.RequestServices.GetRequiredService<ProblemDetailsFactory>();
                var pd = factory.CreateValidationProblemDetails(context.HttpContext, context.ModelState);
                return new ObjectResult(pd) { StatusCode = pd.Status ?? StatusCodes.Status400BadRequest, ContentTypes = { "application/problem+json" } };
            };
        });

        services.AddAutoMapper(cfg => cfg.AddMaps(typeof(Program).Assembly));
        services.AddValidatorsFromAssembly(typeof(Program).Assembly);
    }

    private static void AddCorsConfiguration(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<CorsPolicyOptions>()
            .BindConfiguration(CorsPolicyOptions.SectionName)
            .PostConfigure(options =>
            {
                if (string.IsNullOrWhiteSpace(options.PolicyName))
                {
                    options.PolicyName = "AllowReact";
                }
            })
            .Validate(options =>
            {
                ValidateCorsCredentialsWithOrigins(
                    options,
                    ConfigurationPath.Combine(CorsPolicyOptions.SectionName, nameof(CorsPolicyOptions.Origins)));
                return true;
            })
            .ValidateOnStart();

        var corsPolicyOptions = configuration.GetSection(CorsPolicyOptions.SectionName).Get<CorsPolicyOptions>() ?? new CorsPolicyOptions();
        var resolvedCorsPolicyName = string.IsNullOrWhiteSpace(corsPolicyOptions.PolicyName) ? "AllowReact" : corsPolicyOptions.PolicyName;

        services.AddCors(options =>
        {
            options.AddPolicy(resolvedCorsPolicyName, policy =>
            {
                var origins = corsPolicyOptions.Origins ?? Array.Empty<string>();
                if (origins.Length > 0)
                {
                    policy.WithOrigins(origins);
                }

                var headers = corsPolicyOptions.Headers ?? Array.Empty<string>();
                if (headers.Length > 0) { policy.WithHeaders(headers); } else { policy.AllowAnyHeader(); }

                var methods = corsPolicyOptions.Methods ?? Array.Empty<string>();
                if (methods.Length > 0) { policy.WithMethods(methods); } else { policy.AllowAnyMethod(); }

                if (corsPolicyOptions.AllowCredentials) { policy.AllowCredentials(); }

                policy.WithExposedHeaders("X-Total-Count");
                if (corsPolicyOptions.PreflightMaxAge is { } maxAge) { policy.SetPreflightMaxAge(maxAge); }
            });
        });
    }

    private static void AddForwardedHeadersConfiguration(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<ForwardedHeadersSettings>()
            .BindConfiguration(ForwardedHeadersSettings.SectionName)
            .ValidateOnStart();

        var forwardedHeadersSettings = configuration.GetSection(ForwardedHeadersSettings.SectionName)
            .Get<ForwardedHeadersSettings>() ?? new ForwardedHeadersSettings();

        services.AddOptions<ForwardedHeadersOptions>()
            .Configure<ILoggerFactory>((options, loggerFactory) =>
            {
                var logger = loggerFactory?.CreateLogger("ServiceCollectionExtensions") ?? NullLogger.Instance;
                ConfigureForwardedHeadersOptions(options, forwardedHeadersSettings, logger);
            });
    }

    public static void AddAppServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddDbContext<AppDbContext>(options =>
            options.UseSqlite(configuration.GetConnectionString("DefaultConnection") ?? "Data Source=app.db"));
        services.AddHttpClient();
        services.ConfigureHttpClientDefaults(builder =>
            builder.ConfigurePrimaryHttpMessageHandler(ImportHttpGuard.CreateNoRedirectHandler));
        services.AddSingleton(TimeProvider.System);

        // Typed HTTP client for the Star Wars: Unlimited API.
        services.AddHttpClient<ISWUApiClient, SWUApiClient>(client =>
        {
            client.BaseAddress = new Uri("https://admin.starwarsunlimited.com/api/");
            client.Timeout = TimeSpan.FromMinutes(5);
        }).ConfigurePrimaryHttpMessageHandler(ImportHttpGuard.CreateNoRedirectHandler);

        // Typed HTTP client for Star Wars CCG catalog from swccg-card-json.
        services.AddHttpClient<ISwccgApiClient, SwccgApiClient>(client =>
        {
            client.BaseAddress = new Uri("https://raw.githubusercontent.com/swccgpc/swccg-card-json/main/");
            client.Timeout = TimeSpan.FromMinutes(5);
        }).ConfigurePrimaryHttpMessageHandler(ImportHttpGuard.CreateNoRedirectHandler);

        // Typed HTTP client for Pokémon TCG from TCGdex.
        services.AddHttpClient<IPokemonApiClient, TcgdexApiClient>(client =>
        {
            client.BaseAddress = new Uri("https://api.tcgdex.net/v2/en/");
            client.Timeout = TimeSpan.FromMinutes(5);
        }).ConfigurePrimaryHttpMessageHandler(ImportHttpGuard.CreateNoRedirectHandler);

        services.AddScoped<PokemonTcgdexImporter>();
        services.AddScoped<PokemonDeckValidationService>();
        services.AddScoped<api.Features.Cards.Services.PokemonFilterService>();
        services.AddScoped<api.Features.Cards.Services.PokemonEvolutionService>();
        services.AddScoped<ISourceImporter>(sp => sp.GetRequiredService<PokemonTcgdexImporter>());

        services.AddScoped<ISourceImporter>(sp =>
            new SwuDbImporter(
                sp.GetRequiredService<AppDbContext>(),
                sp.GetRequiredService<ISWUApiClient>(),
                sp.GetRequiredService<ILogger<SwuDbImporter>>(),
                sp.GetRequiredService<TimeProvider>()));
        services.AddScoped<ISourceImporter>(sp =>
            new SwccgJsonImporter(
                sp.GetRequiredService<AppDbContext>(),
                sp.GetRequiredService<ISwccgApiClient>(),
                sp.GetRequiredService<ILogger<SwccgJsonImporter>>(),
                sp.GetRequiredService<TimeProvider>()));
        services.AddScoped<ImporterRegistry>();
        services.AddScoped<FileParser>();
        services.AddScoped<api.Features.Admin.Prices.AdminPricesService>();
        services.AddScoped<api.Features.ImportExport.Services.ImportExportService>();
        services.AddSingleton<AdminSyncExecutionTracker>();
    }

    /// <summary>
    /// Ensures that credentialed CORS policies explicitly specify trusted origins to avoid wildcard usage.
    /// </summary>
    internal static void ValidateCorsCredentialsWithOrigins(
        CorsPolicyOptions corsPolicyOptions,
        string originsConfigPath)
    {
        if (!corsPolicyOptions.AllowCredentials)
        {
            return;
        }

        var origins = corsPolicyOptions.Origins ?? Array.Empty<string>();
        if (origins.Length > 0)
        {
            return;
        }

        throw new InvalidOperationException(
            $"CORS configuration invalid: AllowCredentials requires explicit origins. Configure '{originsConfigPath}' with at least one origin.");
    }

    /// <summary>
    /// Parses a known proxy entry, ensuring invalid IP addresses are surfaced immediately.
    /// </summary>
    internal static IPAddress ParseKnownProxy(
        string? value,
        string configKey,
        ILogger logger)
    {
        if (IPAddress.TryParse(value, out var parsed))
        {
            return parsed;
        }

        var loggedValue = value ?? "<null>";
        logger.LogWarning(
            "Invalid known proxy entry at {ConfigKey}: value '{Value}' is not a valid IP address.",
            configKey,
            loggedValue);

        throw new FormatException(
            $"Forwarded headers known proxy value '{loggedValue}' at '{configKey}' is not a valid IP address.");
    }

    /// <summary>
    /// Parses a known network entry, validating both the IP prefix and the prefix length.
    /// </summary>
    /// <returns>The parsed <see cref="IPNetwork"/>.</returns>
    /// <exception cref="FormatException">
    /// Thrown when the prefix is missing/invalid or the prefix length is outside the valid range for the address family.
    /// </exception>
    internal static IPNetwork ParseKnownNetwork(
        ForwardedHeadersSettings.NetworkEntry? networkEntry,
        string prefixConfigKey,
        string prefixLengthConfigKey,
        ILogger logger)
    {
        if (networkEntry is null)
        {
            logger.LogWarning(
                "Invalid known network entry at {PrefixKey}: entry is null.",
                prefixConfigKey);
            throw new FormatException(
                $"Forwarded headers known network entry at '{prefixConfigKey}' is null.");
        }

        var prefixValue = networkEntry.Prefix ?? "<null>";
        if (!IPAddress.TryParse(networkEntry.Prefix, out var prefixAddress))
        {
            logger.LogWarning(
                "Invalid known network prefix at {ConfigKey}: value '{Value}' is not a valid IP address.",
                prefixConfigKey,
                prefixValue);

            throw new FormatException(
                $"Forwarded headers known network prefix '{prefixValue}' at '{prefixConfigKey}' is not a valid IP address.");
        }

        var prefixLength = networkEntry.PrefixLength;
        var maxPrefix = prefixAddress.AddressFamily switch
        {
            AddressFamily.InterNetwork => 32,
            AddressFamily.InterNetworkV6 => 128,
            _ => -1
        };

        if (prefixLength < 0 || maxPrefix < 0 || prefixLength > maxPrefix)
        {
            logger.LogWarning(
                "Invalid known network prefix length at {ConfigKey}: value '{Value}' is outside the allowed range for {Family}.",
                prefixLengthConfigKey,
                prefixLength,
                prefixAddress.AddressFamily);

            throw new FormatException(
                $"Forwarded headers known network prefix length '{prefixLength}' at '{prefixLengthConfigKey}' is not valid for address family {prefixAddress.AddressFamily}.");
        }

        return new IPNetwork(prefixAddress, prefixLength);
    }

    /// <summary>
    /// Applies forwarded headers configuration while ensuring known proxies and networks are validated.
    /// </summary>
    private static void ConfigureForwardedHeadersOptions(
        ForwardedHeadersOptions options,
        ForwardedHeadersSettings settings,
        ILogger logger)
    {
        options.ForwardedHeaders = settings.ForwardedHeaders;

        if (settings.ForwardLimit.HasValue)
        {
            options.ForwardLimit = settings.ForwardLimit.Value;
        }

        options.KnownProxies.Clear();
        var proxies = settings.KnownProxies ?? Array.Empty<string>();
        for (var i = 0; i < proxies.Length; i++)
        {
            var proxyConfigKey = ConfigurationPath.Combine(
                ForwardedHeadersSettings.SectionName,
                nameof(ForwardedHeadersSettings.KnownProxies),
                i.ToString());

            options.KnownProxies.Add(ParseKnownProxy(proxies[i], proxyConfigKey, logger));
        }

        options.KnownIPNetworks.Clear();
        var networks = settings.KnownNetworks ?? Array.Empty<ForwardedHeadersSettings.NetworkEntry>();
        for (var i = 0; i < networks.Length; i++)
        {
            var prefixKey = ConfigurationPath.Combine(
                ForwardedHeadersSettings.SectionName,
                nameof(ForwardedHeadersSettings.KnownNetworks),
                i.ToString(),
                nameof(ForwardedHeadersSettings.NetworkEntry.Prefix));
            var prefixLengthKey = ConfigurationPath.Combine(
                ForwardedHeadersSettings.SectionName,
                nameof(ForwardedHeadersSettings.KnownNetworks),
                i.ToString(),
                nameof(ForwardedHeadersSettings.NetworkEntry.PrefixLength));

            options.KnownIPNetworks.Add(ParseKnownNetwork(networks[i], prefixKey, prefixLengthKey, logger));
        }
    }
}
