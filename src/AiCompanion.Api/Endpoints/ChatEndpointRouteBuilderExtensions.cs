namespace AiCompanion.Api.Endpoints;

public static class ChatEndpointRouteBuilderExtensions
{
    public static IEndpointRouteBuilder MapChatEndpoints(this IEndpointRouteBuilder app)
    {
        var chatGroup = app.MapGroup("/api/v1/chat");
        chatGroup.MapPost(string.Empty, ChatEndpointHandler.HandleAsync);
        chatGroup.MapGet("/history/{sessionId}", ChatHistoryEndpointHandler.HandleAsync);

        var memoryGroup = app.MapGroup("/api/v1/memory");
        memoryGroup.MapGet("/consent", MemoryEndpointHandler.GetConsentAsync);
        memoryGroup.MapPut("/consent", MemoryEndpointHandler.UpsertConsentAsync);
        memoryGroup.MapGet(string.Empty, MemoryEndpointHandler.ListMemoryAsync);
        memoryGroup.MapPost(string.Empty, MemoryEndpointHandler.CreateMemoryAsync);
        memoryGroup.MapDelete("/{id:int}", MemoryEndpointHandler.DeleteMemoryAsync);
        memoryGroup.MapGet("/audit", MemoryEndpointHandler.ListAuditAsync);

        app.Map("/ws/chat/{sessionId}", WebSocketChatEndpointHandler.HandleAsync);

        return app;
    }
}
