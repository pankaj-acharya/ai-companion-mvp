using System.Text.Json.Serialization;

namespace AiCompanion.Api.Contracts;

public sealed class MemoryConsentResponse
{
    [JsonPropertyName("enabled")]
    public bool Enabled { get; init; }

    [JsonPropertyName("updated_at")]
    public DateTimeOffset UpdatedAt { get; init; }
}
