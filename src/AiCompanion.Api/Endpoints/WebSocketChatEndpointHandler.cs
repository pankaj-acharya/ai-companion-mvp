using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using AiCompanion.Api.Contracts;
using AiCompanion.Api.Data;
using AiCompanion.Api.Services;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.Options;

namespace AiCompanion.Api.Endpoints;

internal static class WebSocketChatEndpointHandler
{
    public static async Task HandleAsync(
        HttpContext context,
        string sessionId,
        AppDbContext db,
        ILlmClient llm,
        IOptions<JsonOptions> jsonOptions,
        ILoggerFactory loggerFactory)
    {
        if (!context.WebSockets.IsWebSocketRequest)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            return;
        }

        var serializerOptions = jsonOptions.Value.SerializerOptions;
        var token = context.Request.Query["token"].ToString();
        if (string.IsNullOrWhiteSpace(token))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new { detail = "Missing websocket token." });
            return;
        }

        var userId = token.Trim();
        var logger = loggerFactory.CreateLogger(typeof(WebSocketChatEndpointHandler));

        using var webSocket = await context.WebSockets.AcceptWebSocketAsync();
        var conversation = await ChatEndpointHelpers.GetOrCreateConversationAsync(db, sessionId, userId, context.RequestAborted);
        if (!string.Equals(conversation.UserId, userId, StringComparison.Ordinal))
        {
            await webSocket.CloseAsync(WebSocketCloseStatus.PolicyViolation, "Forbidden.", context.RequestAborted);
            return;
        }

        while (webSocket.State == WebSocketState.Open)
        {
            var rawPayload = await ChatEndpointHelpers.ReceiveTextAsync(webSocket, context.RequestAborted);
            if (rawPayload is null)
            {
                break;
            }

            WsChatRequest? payload;
            try
            {
                payload = JsonSerializer.Deserialize<WsChatRequest>(rawPayload, serializerOptions);
            }
            catch (JsonException)
            {
                await ChatEndpointHelpers.SendJsonAsync(webSocket, new { type = "error", detail = new[] { "Invalid JSON payload." } }, serializerOptions, context.RequestAborted);
                continue;
            }

            if (payload is null)
            {
                await ChatEndpointHelpers.SendJsonAsync(webSocket, new { type = "error", detail = new[] { "Invalid websocket payload." } }, serializerOptions, context.RequestAborted);
                continue;
            }

            var wsValidationErrors = ChatEndpointHelpers.Validate(payload);
            if (wsValidationErrors is not null)
            {
                var details = wsValidationErrors.SelectMany(entry => entry.Value.Select(message => new { field = entry.Key, message })).ToList();
                await ChatEndpointHelpers.SendJsonAsync(webSocket, new { type = "error", detail = details }, serializerOptions, context.RequestAborted);
                continue;
            }

            var persona = string.IsNullOrWhiteSpace(payload.PersonaId) ? LlmDefaults.DefaultPersona : payload.PersonaId;
            var modelId = string.IsNullOrWhiteSpace(payload.ModelId) ? null : payload.ModelId;
            ChatEndpointHelpers.AddMessage(db, sessionId, "user", payload.Message);
            var promptResult = await ChatEndpointHelpers.BuildPromptWithApprovedMemoryAsync(db, userId, payload.Message, context.RequestAborted);
            if (promptResult.injectedMemoryCount > 0)
            {
                ChatEndpointHelpers.AddMemoryAuditEvent(db, userId, "memory_used", details: $"injected_count={promptResult.injectedMemoryCount}");
            }

            var tokenCount = 0;
            var chunks = new StringBuilder();
            try
            {
                await foreach (var chunk in llm.StreamGenerateAsync(promptResult.prompt, persona, context.RequestAborted, modelId))
                {
                    tokenCount += 1;
                    chunks.Append(chunk);
                    await ChatEndpointHelpers.SendJsonAsync(webSocket, new { type = "token", content = chunk }, serializerOptions, context.RequestAborted);
                }
            }
            catch (HttpRequestException ex)
            {
                await LlmRequestErrorHandler.SendWebSocketErrorAsync(webSocket, ex, logger, sessionId, serializerOptions, context.RequestAborted);
                continue;
            }

            ChatEndpointHelpers.AddMessage(db, sessionId, "assistant", chunks.ToString().Trim());
            conversation.UpdatedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(context.RequestAborted);
            await ChatEndpointHelpers.SendJsonAsync(webSocket, new { type = "done", tokens_used = tokenCount }, serializerOptions, context.RequestAborted);
        }
    }
}
