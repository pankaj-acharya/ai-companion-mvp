namespace AiCompanion.Api.Models;

public sealed class MemoryConsent
{
    public string UserId { get; set; } = string.Empty;

    public bool IsEnabled { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
