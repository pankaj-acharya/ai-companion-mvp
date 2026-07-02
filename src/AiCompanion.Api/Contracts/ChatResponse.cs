using System.Text.Json.Serialization;

namespace AiCompanion.Api.Contracts;

public sealed class ChatResponse
{
    [JsonPropertyName("response")]
    public required string Response { get; init; }

    [JsonPropertyName("session_id")]
    public required string SessionId { get; init; }

    [JsonPropertyName("tokens_used")]
    public required int TokensUsed { get; init; }

    [JsonPropertyName("created_at")]
    public required DateTimeOffset CreatedAt { get; init; }
}