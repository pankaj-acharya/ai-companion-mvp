using AiCompanion.Api.Contracts;
using AiCompanion.Api.Data;
using AiCompanion.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace AiCompanion.Api.Endpoints;

internal static class MemoryEndpointHandler
{
    public static async Task<IResult> GetConsentAsync(
        HttpContext httpContext,
        AppDbContext db,
        CancellationToken cancellationToken)
    {
        var auth = ChatEndpointHelpers.TryGetUserIdFromAuthorizationHeader(httpContext.Request.Headers.Authorization);
        if (auth.errorResult is not null)
        {
            return auth.errorResult;
        }

        var consent = await db.MemoryConsents
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.UserId == auth.userId!, cancellationToken);

        return Results.Ok(new MemoryConsentResponse
        {
            Enabled = consent?.IsEnabled ?? false,
            UpdatedAt = consent?.UpdatedAt ?? DateTimeOffset.UtcNow,
        });
    }

    public static async Task<IResult> UpsertConsentAsync(
        MemoryConsentRequest payload,
        HttpContext httpContext,
        AppDbContext db,
        CancellationToken cancellationToken)
    {
        var validationErrors = ChatEndpointHelpers.Validate(payload);
        if (validationErrors is not null)
        {
            return Results.ValidationProblem(validationErrors, statusCode: StatusCodes.Status422UnprocessableEntity);
        }

        var auth = ChatEndpointHelpers.TryGetUserIdFromAuthorizationHeader(httpContext.Request.Headers.Authorization);
        if (auth.errorResult is not null)
        {
            return auth.errorResult;
        }

        var consent = await db.MemoryConsents.SingleOrDefaultAsync(item => item.UserId == auth.userId!, cancellationToken);
        var now = DateTimeOffset.UtcNow;
        if (consent is null)
        {
            consent = new MemoryConsent
            {
                UserId = auth.userId!,
                IsEnabled = payload.Enabled!.Value,
                UpdatedAt = now,
            };
            db.MemoryConsents.Add(consent);
        }
        else
        {
            consent.IsEnabled = payload.Enabled!.Value;
            consent.UpdatedAt = now;
        }

        ChatEndpointHelpers.AddMemoryAuditEvent(
            db,
            auth.userId!,
            "consent_updated",
            details: payload.Enabled.Value ? "enabled" : "disabled");
        await db.SaveChangesAsync(cancellationToken);

        return Results.Ok(new MemoryConsentResponse
        {
            Enabled = consent.IsEnabled,
            UpdatedAt = consent.UpdatedAt,
        });
    }

    public static async Task<IResult> CreateMemoryAsync(
        MemoryUpsertRequest payload,
        HttpContext httpContext,
        AppDbContext db,
        CancellationToken cancellationToken)
    {
        var validationErrors = ChatEndpointHelpers.Validate(payload);
        if (validationErrors is not null)
        {
            return Results.ValidationProblem(validationErrors, statusCode: StatusCodes.Status422UnprocessableEntity);
        }

        var auth = ChatEndpointHelpers.TryGetUserIdFromAuthorizationHeader(httpContext.Request.Headers.Authorization);
        if (auth.errorResult is not null)
        {
            return auth.errorResult;
        }

        if (!await ChatEndpointHelpers.IsMemoryConsentEnabledAsync(db, auth.userId!, cancellationToken))
        {
            return Results.Json(new { detail = "Memory consent is disabled." }, statusCode: StatusCodes.Status403Forbidden);
        }

        var now = DateTimeOffset.UtcNow;
        var memoryEntry = new MemoryEntry
        {
            UserId = auth.userId!,
            Scope = payload.Scope,
            Content = payload.Content.Trim(),
            IsApproved = payload.IsApproved ?? true,
            CreatedAt = now,
            UpdatedAt = now,
        };

        db.MemoryEntries.Add(memoryEntry);
        await db.SaveChangesAsync(cancellationToken);

        ChatEndpointHelpers.AddMemoryAuditEvent(
            db,
            auth.userId!,
            "memory_created",
            memoryEntryId: memoryEntry.Id,
            details: memoryEntry.Scope);
        await db.SaveChangesAsync(cancellationToken);

        return Results.Ok(new MemoryItemResponse
        {
            Id = memoryEntry.Id,
            Scope = memoryEntry.Scope,
            Content = memoryEntry.Content,
            IsApproved = memoryEntry.IsApproved,
            CreatedAt = memoryEntry.CreatedAt,
            UpdatedAt = memoryEntry.UpdatedAt,
        });
    }

    public static async Task<IResult> ListMemoryAsync(
        HttpContext httpContext,
        AppDbContext db,
        CancellationToken cancellationToken)
    {
        var auth = ChatEndpointHelpers.TryGetUserIdFromAuthorizationHeader(httpContext.Request.Headers.Authorization);
        if (auth.errorResult is not null)
        {
            return auth.errorResult;
        }

        var scope = httpContext.Request.Query["scope"].ToString();
        var approvedOnlyQuery = httpContext.Request.Query["approved_only"].ToString();
        var approvedOnly = string.IsNullOrWhiteSpace(approvedOnlyQuery)
            || !bool.TryParse(approvedOnlyQuery, out var parsedApprovedOnly)
            || parsedApprovedOnly;

        var query = db.MemoryEntries
            .AsNoTracking()
            .Where(item => item.UserId == auth.userId!);
        if (!string.IsNullOrWhiteSpace(scope))
        {
            query = query.Where(item => item.Scope == scope);
        }

        if (approvedOnly)
        {
            query = query.Where(item => item.IsApproved);
        }

        var items = await query
            .OrderByDescending(item => item.Id)
            .Select(item => new MemoryItemResponse
            {
                Id = item.Id,
                Scope = item.Scope,
                Content = item.Content,
                IsApproved = item.IsApproved,
                CreatedAt = item.CreatedAt,
                UpdatedAt = item.UpdatedAt,
            })
            .ToListAsync(cancellationToken);

        return Results.Ok(new MemoryListResponse
        {
            Items = items,
        });
    }

    public static async Task<IResult> DeleteMemoryAsync(
        int id,
        HttpContext httpContext,
        AppDbContext db,
        CancellationToken cancellationToken)
    {
        var auth = ChatEndpointHelpers.TryGetUserIdFromAuthorizationHeader(httpContext.Request.Headers.Authorization);
        if (auth.errorResult is not null)
        {
            return auth.errorResult;
        }

        var memoryEntry = await db.MemoryEntries.SingleOrDefaultAsync(
            item => item.Id == id && item.UserId == auth.userId!,
            cancellationToken);
        if (memoryEntry is null)
        {
            return Results.NotFound(new { detail = "Memory not found." });
        }

        db.MemoryEntries.Remove(memoryEntry);
        ChatEndpointHelpers.AddMemoryAuditEvent(
            db,
            auth.userId!,
            "memory_deleted",
            memoryEntryId: id,
            details: memoryEntry.Scope);
        await db.SaveChangesAsync(cancellationToken);

        return Results.NoContent();
    }

    public static async Task<IResult> ListAuditAsync(
        HttpContext httpContext,
        AppDbContext db,
        CancellationToken cancellationToken)
    {
        var auth = ChatEndpointHelpers.TryGetUserIdFromAuthorizationHeader(httpContext.Request.Headers.Authorization);
        if (auth.errorResult is not null)
        {
            return auth.errorResult;
        }

        var queryPage = httpContext.Request.Query["page"].ToString();
        var queryPageSize = httpContext.Request.Query["page_size"].ToString();
        var page = string.IsNullOrWhiteSpace(queryPage) ? 1 : int.TryParse(queryPage, out var parsedPage) ? parsedPage : int.MinValue;
        var pageSize = string.IsNullOrWhiteSpace(queryPageSize) ? 20 : int.TryParse(queryPageSize, out var parsedPageSize) ? parsedPageSize : int.MinValue;

        var queryErrors = ChatEndpointHelpers.ValidateHistoryQuery(page, pageSize);
        if (queryErrors is not null)
        {
            return Results.ValidationProblem(queryErrors, statusCode: StatusCodes.Status422UnprocessableEntity);
        }

        var total = await db.MemoryAuditEvents
            .AsNoTracking()
            .Where(item => item.UserId == auth.userId!)
            .CountAsync(cancellationToken);

        var events = await db.MemoryAuditEvents
            .AsNoTracking()
            .Where(item => item.UserId == auth.userId!)
            .OrderByDescending(item => item.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(item => new MemoryAuditEventResponse
            {
                Id = item.Id,
                Action = item.Action,
                MemoryEntryId = item.MemoryEntryId,
                Details = item.Details,
                CreatedAt = item.CreatedAt,
            })
            .ToListAsync(cancellationToken);

        return Results.Ok(new MemoryAuditResponse
        {
            Events = events,
            Total = total,
            Page = page,
            PageSize = pageSize,
        });
    }
}
