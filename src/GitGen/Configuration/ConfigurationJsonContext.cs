using System.Text.Json.Serialization;

namespace GitGen.Configuration;

/// <summary>
///     JSON serializer context for GitGen configuration types to support trimming.
/// </summary>
[JsonSerializable(typeof(GitGenSettings))]
[JsonSerializable(typeof(ModelConfiguration))]
[JsonSerializable(typeof(AppSettings))]
[JsonSerializable(typeof(PricingInfo))]
[JsonSerializable(typeof(List<ModelConfiguration>))]
[JsonSerializable(typeof(List<string>))]
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    WriteIndented = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
internal partial class ConfigurationJsonContext : JsonSerializerContext
{
}