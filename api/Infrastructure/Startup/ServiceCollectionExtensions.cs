using api.Authentication;
using api.Common.Errors;
using api.Data;
using api.Importing;
using api.Models;
using api.Shared.Importing;
using AutoMapper;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.IdentityModel.Tokens;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Reflection;
using HttpIPNetwork = Microsoft.AspNetCore.HttpOverrides.IPNetwork;

namespace api.Infrastructure.Startup;

internal static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApiBasics(
        this IServiceCollection services,
        IConfiguration configuration)
    {
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

        services.AddControllers()
            .AddJsonOptions(JsonOptionsConfigurator.Configure);

        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen();

        services.Configure<ApiBehaviorOptions>(options =>
        {
            // Title/Link defaults for automatic 4xx responses
            options.ClientErrorMapping[StatusCodes.Status400BadRequest] = new ClientErrorData
            {
                Link = ProblemTypes.BadRequest.Type,
                Title = ProblemTypes.BadRequest.Title
            };
            options.ClientErrorMapping[StatusCodes.Status404NotFound] = new ClientErrorData
            {
                Link = ProblemTypes.NotFound.Type,
                Title = ProblemTypes.NotFound.Title
            };
            options.ClientErrorMapping[StatusCodes.Status409Conflict] = new ClientErrorData
            {
                Link = ProblemTypes.Conflict.Type,
                Title = ProblemTypes.Conflict.Title
            };

            // Custom payload for model validation failures
            options.InvalidModelStateResponseFactory = context =>
            {
                var factory = context.HttpContext.RequestServices.GetRequiredService<ProblemDetailsFactory>();
                var pd = factory.CreateValidationProblemDetails(context.HttpContext, context.ModelState);
                return new ObjectResult(pd)
                {
                    StatusCode = pd.Status ?? StatusCodes.Status400BadRequest,
                    ContentTypes = { "application/problem+json" }
                };
            };
        });

        services.AddAutoMapper(typeof(Program).Assembly);
        services.AddValidatorsFromAssembly(typeof(Program).Assembly);

        var corsPolicyOptions = configuration.GetSection(CorsPolicyOptions.SectionName)
            .Get<CorsPolicyOptions>() ?? new CorsPolicyOptions();
        ValidateCorsCredentialsWithOrigins(
            corsPolicyOptions,
            ConfigurationPath.Combine(CorsPolicyOptions.SectionName, nameof(CorsPolicyOptions.Origins)));
        var resolvedCorsPolicyName = string.IsNullOrWhiteSpace(corsPolicyOptions.PolicyName)
            ? "AllowReact"
            : corsPolicyOptions.PolicyName;

        services.Configure<CorsPolicyOptions>(options =>
        {
            options.PolicyName = resolvedCorsPolicyName;
            options.Origins = corsPolicyOptions.Origins;
            options.Headers = corsPolicyOptions.Headers;
            options.Methods = corsPolicyOptions.Methods;
            options.AllowCredentials = corsPolicyOptions.AllowCredentials;
            options.PreflightMaxAge = corsPolicyOptions.PreflightMaxAge;
        });

        var forwardedHeadersSettings = configuration.GetSection(ForwardedHeadersSettings.SectionName)
            .Get<ForwardedHeadersSettings>() ?? new ForwardedHeadersSettings();

        services.Configure<ForwardedHeadersSettings>(options =>
        {
            options.ForwardedHeaders = forwardedHeadersSettings.ForwardedHeaders;
            options.ForwardLimit = forwardedHeadersSettings.ForwardLimit;
            options.KnownProxies = forwardedHeadersSettings.KnownProxies ?? Array.Empty<string>();
            options.KnownNetworks = forwardedHeadersSettings.KnownNetworks ?? Array.Empty<ForwardedHeadersSettings.NetworkEntry>();
        });

        services.AddOptions<ForwardedHeadersOptions>()
            .Configure<ILoggerFactory>((options, loggerFactory) =>
            {
                var logger = loggerFactory?.CreateLogger<ServiceCollectionExtensions>()
                    ?? NullLogger.Instance;
                ConfigureForwardedHeadersOptions(options, forwardedHeadersSettings, logger);
            });

        // Explicit HTTPS port for redirects
        services.AddHttpsRedirection(options => options.HttpsPort = 7226);

        // CORS: Vite only
        services.AddCors(options =>
        {
            options.AddPolicy(resolvedCorsPolicyName, policy =>
            {
                var origins = corsPolicyOptions.Origins ?? Array.Empty<string>();
                if (origins.Length > 0)
                {
                    policy.WithOrigins(origins);
                }
                else if (!corsPolicyOptions.AllowCredentials)
                {
                    policy.AllowAnyOrigin();
                }

                var headers = corsPolicyOptions.Headers ?? Array.Empty<string>();
                if (headers.Length > 0)
                {
                    policy.WithHeaders(headers);
                }
                else
                {
                    policy.AllowAnyHeader();
                }

                var methods = corsPolicyOptions.Methods ?? Array.Empty<string>();
                if (methods.Length > 0)
                {
                    policy.WithMethods(methods);
                }
                else
                {
                    policy.AllowAnyMethod();
                }

                if (corsPolicyOptions.AllowCredentials)
                {
                    policy.AllowCredentials();
                }

                if (corsPolicyOptions.PreflightMaxAge is { } maxAge)
                {
                    policy.SetPreflightMaxAge(maxAge);
                }
            });
        });

        return services;
    }

    public static bool AddAppServices(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        services.AddDbContext<AppDbContext>(options => options.UseSqlite("Data Source=app.db"));
        services.AddHttpClient();

        var jwtSection = configuration.GetSection("Jwt");
        var jwtOptions = jwtSection.Get<JwtOptions>() ?? new JwtOptions();

        var configuredKey = jwtOptions.Key;
        if (string.IsNullOrWhiteSpace(configuredKey))
        {
            var envKey = Environment.GetEnvironmentVariable("JWT__KEY");
            if (!string.IsNullOrWhiteSpace(envKey))
            {
                jwtOptions.Key = envKey;
            }
        }

        const string DevFallbackKey = "DevOnly_Minimum_32_Chars_Key_For_Local_Use_1234";
        var usingDevFallbackKey = false;

        if (string.IsNullOrWhiteSpace(jwtOptions.Key) && (environment.IsDevelopment() || environment.IsEnvironment("Testing")))
        {
            jwtOptions.Key = DevFallbackKey;
            usingDevFallbackKey = true;
        }

        var requiresStrongKey = environment.IsProduction() || environment.IsStaging();

        if (requiresStrongKey)
        {
            if (string.IsNullOrWhiteSpace(jwtOptions.Key))
            {
                throw new InvalidOperationException(
                    "JWT signing key is not configured. Set Jwt:Key or JWT__KEY for Production/Staging environments.");
            }

            if (Encoding.UTF8.GetByteCount(jwtOptions.Key) < 32)
            {
                throw new InvalidOperationException(
                    "JWT signing key must be at least 256 bits (32 bytes) when running in Production or Staging.");
            }
        }
        else if (string.IsNullOrWhiteSpace(jwtOptions.Key))
        {
            throw new InvalidOperationException(
                "JWT signing key is not configured. Set Jwt:Key in configuration or provide JWT__KEY.");
        }

        var signingKeyBytes = Encoding.UTF8.GetBytes(jwtOptions.Key);

        services.Configure<JwtOptions>(options =>
        {
            options.Issuer = jwtOptions.Issuer;
            options.Audience = jwtOptions.Audience;
            options.Key = jwtOptions.Key;
            options.AccessTokenLifetimeMinutes = jwtOptions.AccessTokenLifetimeMinutes;
        });

        services.AddSingleton<IJwtTokenService, JwtTokenService>();
        services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();

        services
            .AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultForbidScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = jwtOptions.Issuer,
                    ValidAudience = jwtOptions.Audience,
                    IssuerSigningKey = new SymmetricSecurityKey(signingKeyBytes)
                };
            });

        services.AddAuthorization();

        services.AddScoped<ISourceImporter, ScryfallImporter>();
        services.AddScoped<ISourceImporter, SwccgdbImporter>();
        services.AddScoped<ISourceImporter, LorcanaJsonImporter>();
        services.AddScoped<ISourceImporter, SwuDbImporter>();
        services.AddScoped<ISourceImporter, PokemonTcgImporter>();
        services.AddScoped<ISourceImporter, FabDbImporter>();
        services.AddScoped<ISourceImporter, GuardiansLocalImporter>();
        services.AddScoped<ISourceImporter, DiceMastersDbImporter>();
        services.AddScoped<ISourceImporter, TransformersFmImporter>();
        services.AddScoped<ImporterRegistry>();
        services.AddScoped<FileParser>();

        return usingDevFallbackKey;
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
    /// <returns>The parsed <see cref="HttpIPNetwork"/>.</returns>
    /// <exception cref="FormatException">
    /// Thrown when the prefix is missing/invalid or the prefix length is outside the valid range for the address family.
    /// </exception>
    internal static HttpIPNetwork ParseKnownNetwork(
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

        return new HttpIPNetwork(prefixAddress, prefixLength);
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

        options.KnownNetworks.Clear();
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

            options.KnownNetworks.Add(ParseKnownNetwork(networks[i], prefixKey, prefixLengthKey, logger));
        }
    }
}
