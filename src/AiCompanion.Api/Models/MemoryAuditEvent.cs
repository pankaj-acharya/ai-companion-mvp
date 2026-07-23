namespace AiCompanion.Api.Models;

public sealed class MemoryAuditEvent
{
    public int Id { get; set; }

    public string UserId { get; set; } = string.Empty;

    public string Action { get; set; } = string.Empty;

    public int? MemoryEntryId { get; set; }

    public string? Details { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
