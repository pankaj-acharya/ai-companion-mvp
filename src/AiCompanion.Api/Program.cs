using AiCompanion.Api.Endpoints;
using AiCompanion.Api.Data;
using AiCompanion.Api.Services;
using DotNetEnv;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

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
builder.Services.AddHttpClient<OpenAiLlmClient>((sp, client) =>
{
    var settings = sp.GetRequiredService<AppSettings>();
    client.BaseAddress = new Uri(settings.BaseUrl);
});
builder.Services.AddSingleton<ILlmClient>(sp =>
{
    var settings = sp.GetRequiredService<AppSettings>();
    if (settings.UseMockLlm)
    {
        return new MockLlmClient();
    }

    return sp.GetRequiredService<OpenAiLlmClient>();
});

var app = builder.Build();

var startupSettings = app.Services.GetRequiredService<AppSettings>();
app.Logger.LogInformation(
    "LLM provider mode: {LlmMode}; provider: {Provider}; base_url: {BaseUrl}; model: {Model}",
    startupSettings.UseMockLlm ? "Mock" : "OpenAI",
    startupSettings.Provider,
    startupSettings.BaseUrl,
    startupSettings.Model);

app.UseDefaultFiles();
app.UseStaticFiles();
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
    DbSchemaUpgrader.EnsureMemorySchema(db);
}

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.MapGet("/", () =>
    Results.Ok(new
    {
        name = "AI Companion MVP API",
        ui = "/app/",
        docs = "/docs",
        health = "/health",
        mock_mode = startupSettings.UseMockLlm,
        llm_provider = startupSettings.Provider,
        llm_base_url = startupSettings.BaseUrl,
    }));

app.MapGet("/app", () => Results.File("wwwroot/app/index.html", "text/html"));
app.MapChatEndpoints();

app.Run();

public partial class Program;
