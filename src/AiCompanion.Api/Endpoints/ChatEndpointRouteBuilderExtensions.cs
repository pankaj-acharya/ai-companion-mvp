namespace AiCompanion.Api.Endpoints;

public static class ChatEndpointRouteBuilderExtensions
{
    public static IEndpointRouteBuilder MapChatEndpoints(this IEndpointRouteBuilder app)
    {
        var chatGroup = app.MapGroup("/api/v1/chat");
        chatGroup.MapPost(string.Empty, ChatEndpointHandler.HandleAsync);
        chatGroup.MapGet("/history/{sessionId}", ChatHistoryEndpointHandler.HandleAsync);

        app.Map("/ws/chat/{sessionId}", WebSocketChatEndpointHandler.HandleAsync);

        return app;
    }
}
