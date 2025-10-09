using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using api.Authentication;
using api.Common.Errors;
using api.Data;
using api.Importing;
using api.Models;
using api.Shared.Importing;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;

namespace api.Infrastructure.Startup;

internal static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApiBasics(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddControllers()
            .AddJsonOptions(JsonOptionsConfigurator.Configure)
            .ConfigureApiBehaviorOptions(options =>
            {
                options.InvalidModelStateResponseFactory = context =>
                {
                    var problemDetailsFactory = context.HttpContext.RequestServices
                        .GetRequiredService<ProblemDetailsFactory>();

                    var problemDetails = problemDetailsFactory.CreateValidationProblemDetails(
                        context.HttpContext,
                        context.ModelState);

                    return new ObjectResult(problemDetails)
                    {
                        StatusCode = problemDetails.Status ?? StatusCodes.Status400BadRequest,
                        ContentTypes = { "application/problem+json" }
                    };
                };
            });

        services.AddAutoMapper(typeof(Program).Assembly);
        services.AddValidatorsFromAssembly(typeof(Program).Assembly);

        services.Configure<ProblemDetailsOptions>(options =>
        {
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
        });

        services.AddSingleton<ProblemDetailsFactory, DefaultProblemDetailsFactory>();

        var corsPolicyOptions = configuration.GetSection(CorsPolicyOptions.SectionName)
            .Get<CorsPolicyOptions>() ?? new CorsPolicyOptions();
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

        services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = forwardedHeadersSettings.ForwardedHeaders;

            if (forwardedHeadersSettings.ForwardLimit.HasValue)
            {
                options.ForwardLimit = forwardedHeadersSettings.ForwardLimit.Value;
            }

            options.KnownProxies.Clear();
            foreach (var proxy in forwardedHeadersSettings.KnownProxies ?? Array.Empty<string>())
            {
                if (IPAddress.TryParse(proxy, out var parsed))
                {
                    options.KnownProxies.Add(parsed);
                }
            }

            options.KnownNetworks.Clear();
            foreach (var network in forwardedHeadersSettings.KnownNetworks ?? Array.Empty<ForwardedHeadersSettings.NetworkEntry>())
            {
                if (network?.Prefix is null)
                {
                    continue;
                }

                if (!IPAddress.TryParse(network.Prefix, out var prefixAddress))
                {
                    throw new Exception($"Invalid network prefix in ForwardedHeadersSettings: '{network.Prefix}' is not a valid IP address.");
                }

                var prefixLength = network.PrefixLength;
                if (prefixLength < 0)
                {
                    continue;
                }

                var maxPrefix = prefixAddress.AddressFamily switch
                {
                    AddressFamily.InterNetwork => 32,
                    AddressFamily.InterNetworkV6 => 128,
                    _ => -1
                };

                if (maxPrefix >= 0 && prefixLength > maxPrefix)
                {
                    continue;
                }

                options.KnownNetworks.Add(new IPNetwork(prefixAddress, prefixLength));
            }
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
}
