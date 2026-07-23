using System.Text.Json.Serialization;

namespace AiCompanion.Api.Contracts;

public sealed class MemoryAuditResponse
{
    [JsonPropertyName("events")]
    public IReadOnlyList<MemoryAuditEventResponse> Events { get; init; } = [];

    [JsonPropertyName("total")]
    public int Total { get; init; }

    [JsonPropertyName("page")]
    public int Page { get; init; }

    [JsonPropertyName("page_size")]
    public int PageSize { get; init; }
}
