namespace AiCompanion.Api.Models;

public sealed class Conversation
{
    public string Id { get; set; } = string.Empty;

    public string UserId { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public List<Message> Messages { get; set; } = [];
}