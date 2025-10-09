using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;

namespace api.Infrastructure.Startup;

internal static class JsonOptionsConfigurator
{
    public static void Configure(JsonOptions options)
    {
        Configure(options.JsonSerializerOptions);
    }

    public static void Configure(JsonSerializerOptions serializerOptions)
    {
        serializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        serializerOptions.DictionaryKeyPolicy = JsonNamingPolicy.CamelCase;
        serializerOptions.PropertyNameCaseInsensitive = true;
        serializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
        serializerOptions.NumberHandling = JsonNumberHandling.Strict;

        EnsureEnumConverter(serializerOptions);
    }

    public static JsonSerializerOptions CreateSerializerOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        Configure(options);
        return options;
    }

    private static void EnsureEnumConverter(JsonSerializerOptions options)
    {
        if (!options.Converters.OfType<JsonStringEnumConverter>().Any())
        {
            options.Converters.Add(new JsonStringEnumConverter());
        }
    }
}
