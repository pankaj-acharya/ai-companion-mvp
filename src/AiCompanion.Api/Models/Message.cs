namespace AiCompanion.Api.Models;

public sealed class Message
{
    public int Id { get; set; }

    public string ConversationId { get; set; } = string.Empty;

    public string Role { get; set; } = string.Empty;

    public string Content { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }

    public Conversation? Conversation { get; set; }
}