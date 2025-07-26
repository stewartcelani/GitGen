using System.Text.Json.Serialization;

namespace GitGen.Providers.OpenAI;

/// <summary>
///     JSON serializer context for OpenAI provider types.
///     Enables source generation for better performance and trimming compatibility.
/// </summary>
[JsonSerializable(typeof(OpenAIRequest))]
[JsonSerializable(typeof(OpenAIResponse))]
[JsonSerializable(typeof(Message))]
[JsonSerializable(typeof(Choice))]
[JsonSerializable(typeof(Usage))]
[JsonSerializable(typeof(ApiParameters))]
public partial class OpenAIJsonContext : JsonSerializerContext
{
}