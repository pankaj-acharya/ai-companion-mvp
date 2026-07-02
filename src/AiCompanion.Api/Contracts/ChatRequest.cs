using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace AiCompanion.Api.Contracts;

public sealed class ChatRequest
{
    [JsonPropertyName("session_id")]
    [Required(AllowEmptyStrings = false)]
    [StringLength(128, MinimumLength = 1)]
    public string SessionId { get; init; } = string.Empty;

    [JsonPropertyName("message")]
    [Required(AllowEmptyStrings = false)]
    [StringLength(8000, MinimumLength = 1)]
    public string Message { get; init; } = string.Empty;

    [JsonPropertyName("persona_id")]
    [StringLength(128, MinimumLength = 1)]
    public string? PersonaId { get; init; }
}