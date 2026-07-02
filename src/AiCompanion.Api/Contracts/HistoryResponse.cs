using System.Text.Json.Serialization;

namespace AiCompanion.Api.Contracts;

public sealed class HistoryResponse
{
    [JsonPropertyName("messages")]
    public required IReadOnlyList<HistoryMessage> Messages { get; init; }

    [JsonPropertyName("total")]
    public required int Total { get; init; }

    [JsonPropertyName("page")]
    public required int Page { get; init; }

    [JsonPropertyName("page_size")]
    public required int PageSize { get; init; }
}