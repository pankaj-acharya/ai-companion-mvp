using System.Text.Json.Serialization;

namespace AiCompanion.Api.Contracts;

public sealed class MemoryAuditEventResponse
{
    [JsonPropertyName("id")]
    public int Id { get; init; }

    [JsonPropertyName("action")]
    public string Action { get; init; } = string.Empty;

    [JsonPropertyName("memory_entry_id")]
    public int? MemoryEntryId { get; init; }

    [JsonPropertyName("details")]
    public string? Details { get; init; }

    [JsonPropertyName("created_at")]
    public DateTimeOffset CreatedAt { get; init; }
}
