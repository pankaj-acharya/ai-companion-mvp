using System.ComponentModel.DataAnnotations;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using AiCompanion.Api.Data;
using AiCompanion.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace AiCompanion.Api.Endpoints;

internal static class ChatEndpointHelpers
{
    public static async Task<Conversation> GetOrCreateConversationAsync(AppDbContext db, string sessionId, string userId, CancellationToken cancellationToken)
    {
        var conversation = await db.Conversations.SingleOrDefaultAsync(item => item.Id == sessionId, cancellationToken);
        if (conversation is not null)
        {
            return conversation;
        }

        conversation = new Conversation
        {
            Id = sessionId,
            UserId = userId,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };
        db.Conversations.Add(conversation);
        await db.SaveChangesAsync(cancellationToken);
        return conversation;
    }

    public static void AddMessage(AppDbContext db, string sessionId, string role, string content)
    {
        db.Messages.Add(new Message
        {
            ConversationId = sessionId,
            Role = role,
            Content = content,
            CreatedAt = DateTimeOffset.UtcNow,
        });
    }

    public static void AddMemoryAuditEvent(
        AppDbContext db,
        string userId,
        string action,
        int? memoryEntryId = null,
        string? details = null)
    {
        db.MemoryAuditEvents.Add(new MemoryAuditEvent
        {
            UserId = userId,
            Action = action,
            MemoryEntryId = memoryEntryId,
            Details = details,
            CreatedAt = DateTimeOffset.UtcNow,
        });
    }

    public static async Task<bool> IsMemoryConsentEnabledAsync(AppDbContext db, string userId, CancellationToken cancellationToken)
    {
        var consent = await db.MemoryConsents
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.UserId == userId, cancellationToken);
        return consent?.IsEnabled ?? false;
    }

    public static async Task<(string prompt, int injectedMemoryCount)> BuildPromptWithApprovedMemoryAsync(
        AppDbContext db,
        string userId,
        string message,
        CancellationToken cancellationToken)
    {
        if (!await IsMemoryConsentEnabledAsync(db, userId, cancellationToken))
        {
            return (message, 0);
        }

        var memories = await db.MemoryEntries
            .AsNoTracking()
            .Where(item => item.UserId == userId && item.IsApproved)
            .OrderByDescending(item => item.Id)
            .Take(8)
            .Select(item => new { item.Scope, item.Content })
            .ToListAsync(cancellationToken);

        if (memories.Count == 0)
        {
            return (message, 0);
        }

        var memoryLines = string.Join(
            Environment.NewLine,
            memories.Select(item => $"- [{item.Scope}] {item.Content}"));

        var prompt = $"Use this approved user memory for personalization when relevant:{Environment.NewLine}{memoryLines}{Environment.NewLine}{Environment.NewLine}User message: {message}";
        return (prompt, memories.Count);
    }

    public static Dictionary<string, string[]>? Validate<T>(T payload)
    {
        var validationResults = new List<ValidationResult>();
        var validationContext = new ValidationContext(payload!);
        var isValid = Validator.TryValidateObject(payload!, validationContext, validationResults, validateAllProperties: true);
        if (isValid)
        {
            return null;
        }

        return validationResults
            .GroupBy(result => JsonNamingPolicy.SnakeCaseLower.ConvertName(result.MemberNames.FirstOrDefault() ?? string.Empty), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group.Select(result => result.ErrorMessage ?? "Validation failed.").Distinct(StringComparer.Ordinal).ToArray(),
                StringComparer.OrdinalIgnoreCase);
    }

    public static Dictionary<string, string[]>? ValidateHistoryQuery(int page, int pageSize)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
        if (page < 1)
        {
            errors["page"] = new[] { "The field page must be between 1 and 2147483647." };
        }

        if (pageSize is < 1 or > 200)
        {
            errors["page_size"] = new[] { "The field page_size must be between 1 and 200." };
        }

        return errors.Count == 0 ? null : errors;
    }

    public static (string? userId, IResult? errorResult) TryGetUserIdFromAuthorizationHeader(string? authorizationHeader)
    {
        if (string.IsNullOrWhiteSpace(authorizationHeader))
        {
            return (null, Results.Json(new { detail = "Missing Authorization header." }, statusCode: StatusCodes.Status401Unauthorized));
        }

        const string prefix = "Bearer ";
        if (!authorizationHeader.StartsWith(prefix, StringComparison.Ordinal))
        {
            return (null, Results.Json(new { detail = "Authorization must use Bearer token." }, statusCode: StatusCodes.Status401Unauthorized));
        }

        var userId = authorizationHeader[prefix.Length..].Trim();
        if (string.IsNullOrWhiteSpace(userId))
        {
            return (null, Results.Json(new { detail = "Bearer token must not be empty." }, statusCode: StatusCodes.Status401Unauthorized));
        }

        return (userId, null);
    }

    public static async Task<string?> ReceiveTextAsync(WebSocket socket, CancellationToken cancellationToken)
    {
        const int maxMessageBytes = 64 * 1024;
        var buffer = new byte[4096];
        using var payload = new MemoryStream();

        while (true)
        {
            var result = await socket.ReceiveAsync(buffer, cancellationToken);
            if (result.MessageType == WebSocketMessageType.Close)
            {
                return null;
            }

            if (result.MessageType != WebSocketMessageType.Text)
            {
                await socket.CloseAsync(WebSocketCloseStatus.InvalidMessageType, "Only text messages are supported.", cancellationToken);
                return null;
            }

            if (payload.Length + result.Count > maxMessageBytes)
            {
                await socket.CloseAsync(WebSocketCloseStatus.MessageTooBig, "Payload too large.", cancellationToken);
                return null;
            }

            payload.Write(buffer, 0, result.Count);
            if (result.EndOfMessage)
            {
                break;
            }
        }

        return Encoding.UTF8.GetString(payload.ToArray());
    }

    public static Task SendJsonAsync<T>(WebSocket socket, T payload, JsonSerializerOptions serializerOptions, CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(payload, serializerOptions);
        var bytes = Encoding.UTF8.GetBytes(json);
        return socket.SendAsync(bytes, WebSocketMessageType.Text, endOfMessage: true, cancellationToken);
    }
}
