using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using AiCompanion.Api.Contracts;
using AiCompanion.Api.Data;
using AiCompanion.Api.Models;
using AiCompanion.Api.Services;
using DotNetEnv;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

Env.TraversePath().Load();
var builder = WebApplication.CreateBuilder(args);

builder.Services.ConfigureHttpJsonOptions(options =>
{
	options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower;
	options.SerializerOptions.DictionaryKeyPolicy = JsonNamingPolicy.SnakeCaseLower;
});
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddProblemDetails();

builder.Services.AddSingleton(sp => AppSettings.FromConfiguration(sp.GetRequiredService<IConfiguration>()));
builder.Services.AddDbContext<AppDbContext>((sp, options) =>
{
	var configuration = sp.GetRequiredService<IConfiguration>();
	options.UseSqlite(AppSettings.GetConnectionString(configuration));
});
builder.Services.AddHttpClient<ILlmClient, OpenAiLlmClient>(client =>
{
	client.BaseAddress = new Uri("https://api.openai.com/v1/");
});

var app = builder.Build();

app.UseWebSockets();
app.UseSwagger();
app.UseSwaggerUI(options =>
{
	options.RoutePrefix = "docs";
	options.SwaggerEndpoint("/swagger/v1/swagger.json", "AI Companion MVP API");
});

using (var scope = app.Services.CreateScope())
{
	var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
	db.Database.EnsureCreated();
}

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.MapPost("/api/v1/chat", async Task<IResult> (
	ChatRequest payload,
	HttpContext httpContext,
	AppDbContext db,
	ILlmClient llm,
	CancellationToken cancellationToken) =>
{
	var validationErrors = Validate(payload);
	if (validationErrors is not null)
	{
		return Results.ValidationProblem(validationErrors, statusCode: StatusCodes.Status422UnprocessableEntity);
	}

	var userId = TryGetUserIdFromAuthorizationHeader(httpContext.Request.Headers.Authorization);
	if (userId.errorResult is not null)
	{
		return userId.errorResult;
	}

	var persona = string.IsNullOrWhiteSpace(payload.PersonaId) ? LlmDefaults.DefaultPersona : payload.PersonaId;
	var conversation = await GetOrCreateConversationAsync(db, payload.SessionId, userId.userId!, cancellationToken);
	if (!string.Equals(conversation.UserId, userId.userId, StringComparison.Ordinal))
	{
		return Results.StatusCode(StatusCodes.Status403Forbidden);
	}

	AddMessage(db, payload.SessionId, "user", payload.Message);
	var result = await llm.GenerateAsync(payload.Message, persona, cancellationToken);
	AddMessage(db, payload.SessionId, "assistant", result.Text);
	conversation.UpdatedAt = DateTimeOffset.UtcNow;
	await db.SaveChangesAsync(cancellationToken);

	return Results.Ok(new ChatResponse
	{
		Response = result.Text,
		SessionId = payload.SessionId,
		TokensUsed = result.TokensUsed,
		CreatedAt = DateTimeOffset.UtcNow,
	});
});

app.MapGet("/api/v1/chat/history/{sessionId}", async Task<IResult> (
	string sessionId,
	HttpContext httpContext,
	AppDbContext db,
	CancellationToken cancellationToken) =>
{
	var queryPage = httpContext.Request.Query["page"].ToString();
	var queryPageSize = httpContext.Request.Query["page_size"].ToString();
	var page = string.IsNullOrWhiteSpace(queryPage) ? 1 : int.TryParse(queryPage, out var parsedPage) ? parsedPage : int.MinValue;
	var pageSize = string.IsNullOrWhiteSpace(queryPageSize) ? 20 : int.TryParse(queryPageSize, out var parsedPageSize) ? parsedPageSize : int.MinValue;

	var queryErrors = ValidateHistoryQuery(page, pageSize);
	if (queryErrors is not null)
	{
		return Results.ValidationProblem(queryErrors, statusCode: StatusCodes.Status422UnprocessableEntity);
	}

	var userId = TryGetUserIdFromAuthorizationHeader(httpContext.Request.Headers.Authorization);
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
});

app.Map("/ws/chat/{sessionId}", async (HttpContext context, string sessionId, AppDbContext db, ILlmClient llm, IOptions<Microsoft.AspNetCore.Http.Json.JsonOptions> jsonOptions) =>
{
	if (!context.WebSockets.IsWebSocketRequest)
	{
		context.Response.StatusCode = StatusCodes.Status400BadRequest;
		return;
	}

	var token = context.Request.Query["token"].ToString();
	if (string.IsNullOrWhiteSpace(token))
	{
		context.Response.StatusCode = StatusCodes.Status401Unauthorized;
		await context.Response.WriteAsJsonAsync(new { detail = "Missing websocket token." });
		return;
	}

	using var webSocket = await context.WebSockets.AcceptWebSocketAsync();
	var conversation = await GetOrCreateConversationAsync(db, sessionId, token.Trim(), context.RequestAborted);
	if (!string.Equals(conversation.UserId, token.Trim(), StringComparison.Ordinal))
	{
		await webSocket.CloseAsync(WebSocketCloseStatus.PolicyViolation, "Forbidden.", context.RequestAborted);
		return;
	}

	while (webSocket.State == WebSocketState.Open)
	{
		var rawPayload = await ReceiveTextAsync(webSocket, context.RequestAborted);
		if (rawPayload is null)
		{
			break;
		}

		WsChatRequest? payload;
		try
		{
			payload = JsonSerializer.Deserialize<WsChatRequest>(rawPayload, jsonOptions.Value.SerializerOptions);
		}
		catch (JsonException)
		{
			await SendJsonAsync(webSocket, new { type = "error", detail = new[] { "Invalid JSON payload." } }, jsonOptions.Value.SerializerOptions, context.RequestAborted);
			continue;
		}

		if (payload is null)
		{
			await SendJsonAsync(webSocket, new { type = "error", detail = new[] { "Invalid websocket payload." } }, jsonOptions.Value.SerializerOptions, context.RequestAborted);
			continue;
		}

		var wsValidationErrors = Validate(payload);
		if (wsValidationErrors is not null)
		{
			var details = wsValidationErrors.SelectMany(entry => entry.Value.Select(message => new { field = entry.Key, message })).ToList();
			await SendJsonAsync(webSocket, new { type = "error", detail = details }, jsonOptions.Value.SerializerOptions, context.RequestAborted);
			continue;
		}

		var persona = string.IsNullOrWhiteSpace(payload.PersonaId) ? LlmDefaults.DefaultPersona : payload.PersonaId;
		AddMessage(db, sessionId, "user", payload.Message);

		var tokenCount = 0;
		var chunks = new StringBuilder();
		await foreach (var chunk in llm.StreamGenerateAsync(payload.Message, persona, context.RequestAborted))
		{
			tokenCount += 1;
			chunks.Append(chunk);
			await SendJsonAsync(webSocket, new { type = "token", content = chunk }, jsonOptions.Value.SerializerOptions, context.RequestAborted);
		}

		AddMessage(db, sessionId, "assistant", chunks.ToString().Trim());
		conversation.UpdatedAt = DateTimeOffset.UtcNow;
		await db.SaveChangesAsync(context.RequestAborted);
		await SendJsonAsync(webSocket, new { type = "done", tokens_used = tokenCount }, jsonOptions.Value.SerializerOptions, context.RequestAborted);
	}
});

app.Run();

static async Task<Conversation> GetOrCreateConversationAsync(AppDbContext db, string sessionId, string userId, CancellationToken cancellationToken)
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

static void AddMessage(AppDbContext db, string sessionId, string role, string content)
{
	db.Messages.Add(new Message
	{
		ConversationId = sessionId,
		Role = role,
		Content = content,
		CreatedAt = DateTimeOffset.UtcNow,
	});
}

static Dictionary<string, string[]>? Validate<T>(T payload)
{
	var validationResults = new List<ValidationResult>();
	var validationContext = new ValidationContext(payload!);
	var isValid = Validator.TryValidateObject(payload!, validationContext, validationResults, validateAllProperties: true);
	if (isValid)
	{
		return null;
	}

	return validationResults
		.GroupBy(result => result.MemberNames.FirstOrDefault() ?? string.Empty, StringComparer.OrdinalIgnoreCase)
		.ToDictionary(
			group => group.Key,
			group => group.Select(result => result.ErrorMessage ?? "Validation failed.").Distinct(StringComparer.Ordinal).ToArray(),
			StringComparer.OrdinalIgnoreCase);
}

static Dictionary<string, string[]>? ValidateHistoryQuery(int page, int pageSize)
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

static (string? userId, IResult? errorResult) TryGetUserIdFromAuthorizationHeader(string? authorizationHeader)
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

static async Task<string?> ReceiveTextAsync(WebSocket socket, CancellationToken cancellationToken)
{
	var buffer = new byte[4096];
	using var payload = new MemoryStream();

	while (true)
	{
		var result = await socket.ReceiveAsync(buffer, cancellationToken);
		if (result.MessageType == WebSocketMessageType.Close)
		{
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

static Task SendJsonAsync<T>(WebSocket socket, T payload, JsonSerializerOptions serializerOptions, CancellationToken cancellationToken)
{
	var json = JsonSerializer.Serialize(payload, serializerOptions);
	var bytes = Encoding.UTF8.GetBytes(json);
	return socket.SendAsync(bytes, WebSocketMessageType.Text, endOfMessage: true, cancellationToken);
}

public partial class Program;
