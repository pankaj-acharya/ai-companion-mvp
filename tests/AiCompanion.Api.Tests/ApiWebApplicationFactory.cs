using AiCompanion.Api.Data;
using AiCompanion.Api.Services;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System.Runtime.CompilerServices;

namespace AiCompanion.Api.Tests;

public sealed class ApiWebApplicationFactory : WebApplicationFactory<Program>, IDisposable
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");

    public ApiWebApplicationFactory()
    {
        _connection.Open();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, configurationBuilder) =>
        {
            configurationBuilder.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["OpenAI:ApiKey"] = "test-key",
                ["OpenAI:Model"] = "gpt-4o",
                ["OpenAI:Temperature"] = "0.2",
                ["OpenAI:MaxTokens"] = "300",
                ["ConnectionStrings:DefaultConnection"] = "Data Source=:memory:",
            });
        });

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.RemoveAll<AppDbContext>();
            services.RemoveAll<ILlmClient>();

            services.AddDbContext<AppDbContext>(options => options.UseSqlite(_connection));
            services.AddSingleton<ILlmClient, TestLlmClient>();
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            _connection.Dispose();
        }
    }

    private sealed class TestLlmClient : ILlmClient
    {
        public Task<LlmResult> GenerateAsync(string message, string persona, CancellationToken cancellationToken)
        {
            return Task.FromResult(new LlmResult($"reply:{persona}:{message}", 11));
        }

        public async IAsyncEnumerable<string> StreamGenerateAsync(string message, string persona, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            foreach (var item in $"reply:{persona}:{message}".Split(':', StringSplitOptions.None))
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return item;
                await Task.Yield();
            }
        }
    }
}