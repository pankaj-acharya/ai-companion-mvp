using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace AiCompanion.Api.Contracts;

public sealed class MemoryUpsertRequest
{
    [JsonPropertyName("scope")]
    [Required(AllowEmptyStrings = false)]
    [RegularExpression("^(short_term|long_term)$", ErrorMessage = "The field scope must be one of: short_term, long_term.")]
    public string Scope { get; init; } = "long_term";

    [JsonPropertyName("content")]
    [Required(AllowEmptyStrings = false)]
    [StringLength(1000, MinimumLength = 1)]
    public string Content { get; init; } = string.Empty;

    [JsonPropertyName("is_approved")]
    public bool? IsApproved { get; init; }
}
