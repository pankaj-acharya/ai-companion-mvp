using AiCompanion.Api.Contracts;
using AiCompanion.Api.Data;
using AiCompanion.Api.Services;

namespace AiCompanion.Api.Endpoints;

internal static class ChatEndpointHandler
{
    public static async Task<IResult> HandleAsync(
        ChatRequest payload,
        HttpContext httpContext,
        AppDbContext db,
        ILlmClient llm,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var validationErrors = ChatEndpointHelpers.Validate(payload);
        if (validationErrors is not null)
        {
            return Results.ValidationProblem(validationErrors, statusCode: StatusCodes.Status422UnprocessableEntity);
        }

        var userId = ChatEndpointHelpers.TryGetUserIdFromAuthorizationHeader(httpContext.Request.Headers.Authorization);
        if (userId.errorResult is not null)
        {
            return userId.errorResult;
        }

        var persona = string.IsNullOrWhiteSpace(payload.PersonaId) ? LlmDefaults.DefaultPersona : payload.PersonaId;
        var modelId = string.IsNullOrWhiteSpace(payload.ModelId) ? null : payload.ModelId;
        var conversation = await ChatEndpointHelpers.GetOrCreateConversationAsync(db, payload.SessionId, userId.userId!, cancellationToken);
        if (!string.Equals(conversation.UserId, userId.userId, StringComparison.Ordinal))
        {
            return Results.Json(new { detail = "Session belongs to a different user." }, statusCode: StatusCodes.Status403Forbidden);
        }

        ChatEndpointHelpers.AddMessage(db, payload.SessionId, "user", payload.Message);

        var promptResult = await ChatEndpointHelpers.BuildPromptWithApprovedMemoryAsync(db, userId.userId!, payload.Message, cancellationToken);
        if (promptResult.injectedMemoryCount > 0)
        {
            ChatEndpointHelpers.AddMemoryAuditEvent(db, userId.userId!, "memory_used", details: $"injected_count={promptResult.injectedMemoryCount}");
        }

        LlmResult result;
        try
        {
            result = await llm.GenerateAsync(promptResult.prompt, persona, cancellationToken, modelId);
        }
        catch (HttpRequestException ex)
        {
            var logger = loggerFactory.CreateLogger(typeof(ChatEndpointHandler));
            return LlmRequestErrorHandler.ToHttpResult(ex, logger, payload.SessionId);
        }

        ChatEndpointHelpers.AddMessage(db, payload.SessionId, "assistant", result.Text);
        conversation.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);

        return Results.Ok(new ChatResponse
        {
            Response = result.Text,
            SessionId = payload.SessionId,
            TokensUsed = result.TokensUsed,
            CreatedAt = DateTimeOffset.UtcNow,
        });
    }
}
