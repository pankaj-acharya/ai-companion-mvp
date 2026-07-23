using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace AiCompanion.Api.Contracts;

public sealed class MemoryConsentRequest
{
    [JsonPropertyName("enabled")]
    [Required]
    public bool? Enabled { get; init; }
}
