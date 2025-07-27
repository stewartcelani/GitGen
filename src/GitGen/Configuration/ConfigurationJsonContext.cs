using System.Text.Json.Serialization;

namespace GitGen.Configuration;

/// <summary>
///     JSON serializer context for GitGen configuration types to support trimming.
/// </summary>
[JsonSerializable(typeof(GitGenSettings))]
[JsonSerializable(typeof(ModelConfiguration))]
[JsonSerializable(typeof(AppSettings))]
[JsonSerializable(typeof(PricingInfo))]
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    WriteIndented = true)]
internal partial class ConfigurationJsonContext : JsonSerializerContext
{
}