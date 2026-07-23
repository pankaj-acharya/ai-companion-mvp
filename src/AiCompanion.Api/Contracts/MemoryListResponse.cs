using System.Text.Json.Serialization;

namespace AiCompanion.Api.Contracts;

public sealed class MemoryListResponse
{
    [JsonPropertyName("items")]
    public IReadOnlyList<MemoryItemResponse> Items { get; init; } = [];
}
