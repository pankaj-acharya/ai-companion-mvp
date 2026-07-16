using System.Net;
using System.Net.WebSockets;
using System.Text.Json;

namespace AiCompanion.Api.Endpoints;

internal static class LlmRequestErrorHandler
{
    private const string RateLimitDetail = "LLM provider rate limit reached. Please retry in a few seconds.";
    private const string RequestFailedDetail = "LLM provider request failed. Please try again shortly.";

    public static IResult ToHttpResult(HttpRequestException exception, ILogger logger, string sessionId)
    {
        var error = Map(exception);
        logger.LogWarning(exception, error.isRateLimit
            ? "LLM provider rate limited chat request for session {SessionId}"
            : "LLM provider request failed for session {SessionId}", sessionId);

        return Results.Json(new { detail = error.detail }, statusCode: error.statusCode);
    }

    public static Task SendWebSocketErrorAsync(WebSocket webSocket, HttpRequestException exception, ILogger logger, string sessionId, JsonSerializerOptions serializerOptions, CancellationToken cancellationToken)
    {
        var error = Map(exception);
        logger.LogWarning(exception, error.isRateLimit
            ? "LLM provider rate limited websocket chat for session {SessionId}"
            : "LLM provider request failed over websocket for session {SessionId}", sessionId);

        return ChatEndpointHelpers.SendJsonAsync(webSocket, new { type = "error", detail = new[] { error.detail } }, serializerOptions, cancellationToken);
    }

    internal static (int statusCode, string detail, bool isRateLimit) Map(HttpRequestException exception)
    {
        return exception.StatusCode == HttpStatusCode.TooManyRequests
            ? (StatusCodes.Status429TooManyRequests, RateLimitDetail, true)
            : (StatusCodes.Status502BadGateway, RequestFailedDetail, false);
    }
}
