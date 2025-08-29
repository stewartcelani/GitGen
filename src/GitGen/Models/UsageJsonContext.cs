using System.Text.Json.Serialization;
using GitGen.Models;

namespace GitGen.Models;

/// <summary>
///     JSON serialization context for usage tracking models.
///     Enables source generation for AOT/trimming compatibility.
/// </summary>
[JsonSerializable(typeof(UsageEntry))]
[JsonSerializable(typeof(ModelInfo))]
[JsonSerializable(typeof(TokenUsage))]
[JsonSerializable(typeof(CostInfo))]
[JsonSourceGenerationOptions(
    WriteIndented = false,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
internal partial class UsageJsonContext : JsonSerializerContext
{
}