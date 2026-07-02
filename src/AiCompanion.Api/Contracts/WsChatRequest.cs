using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace AiCompanion.Api.Contracts;

public sealed class WsChatRequest
{
    [JsonPropertyName("message")]
    [Required(AllowEmptyStrings = false)]
    [StringLength(8000, MinimumLength = 1)]
    public string Message { get; init; } = string.Empty;

    [JsonPropertyName("persona_id")]
    [StringLength(128, MinimumLength = 1)]
    public string? PersonaId { get; init; }
}