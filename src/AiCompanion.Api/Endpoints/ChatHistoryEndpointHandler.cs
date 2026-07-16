using AiCompanion.Api.Contracts;
using AiCompanion.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace AiCompanion.Api.Endpoints;

internal static class ChatHistoryEndpointHandler
{
    public static async Task<IResult> HandleAsync(
        string sessionId,
        HttpContext httpContext,
        AppDbContext db,
        CancellationToken cancellationToken)
    {
        var queryPage = httpContext.Request.Query["page"].ToString();
        var queryPageSize = httpContext.Request.Query["page_size"].ToString();
        var page = string.IsNullOrWhiteSpace(queryPage) ? 1 : int.TryParse(queryPage, out var parsedPage) ? parsedPage : int.MinValue;
        var pageSize = string.IsNullOrWhiteSpace(queryPageSize) ? 20 : int.TryParse(queryPageSize, out var parsedPageSize) ? parsedPageSize : int.MinValue;

        var queryErrors = ChatEndpointHelpers.ValidateHistoryQuery(page, pageSize);
        if (queryErrors is not null)
        {
            return Results.ValidationProblem(queryErrors, statusCode: StatusCodes.Status422UnprocessableEntity);
        }

        var userId = ChatEndpointHelpers.TryGetUserIdFromAuthorizationHeader(httpContext.Request.Headers.Authorization);
        if (userId.errorResult is not null)
        {
            return userId.errorResult;
        }

        var conversation = await db.Conversations
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == sessionId && item.UserId == userId.userId, cancellationToken);

        if (conversation is null)
        {
            return Results.NotFound(new { detail = "Session not found." });
        }

        var total = await db.Messages
            .AsNoTracking()
            .Where(item => item.ConversationId == sessionId)
            .CountAsync(cancellationToken);

        var messages = await db.Messages
            .AsNoTracking()
            .Where(item => item.ConversationId == sessionId)
            .OrderBy(item => item.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(item => new HistoryMessage
            {
                Role = item.Role,
                Content = item.Content,
                CreatedAt = item.CreatedAt,
            })
            .ToListAsync(cancellationToken);

        return Results.Ok(new HistoryResponse
        {
            Messages = messages,
            Total = total,
            Page = page,
            PageSize = pageSize,
        });
    }
}
