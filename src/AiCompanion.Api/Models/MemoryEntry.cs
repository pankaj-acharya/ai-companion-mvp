namespace AiCompanion.Api.Models;

public sealed class MemoryEntry
{
    public int Id { get; set; }

    public string UserId { get; set; } = string.Empty;

    public string Scope { get; set; } = "long_term";

    public string Content { get; set; } = string.Empty;

    public bool IsApproved { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
