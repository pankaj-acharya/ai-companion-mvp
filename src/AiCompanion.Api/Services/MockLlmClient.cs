using System.Runtime.CompilerServices;

namespace AiCompanion.Api.Services;

public sealed class MockLlmClient : ILlmClient
{
    public Task<LlmResult> GenerateAsync(string message, string persona, CancellationToken cancellationToken)
    {
        var text = $"mock-reply:{persona}:{message}";
        var tokens = CountTokens(message);
        return Task.FromResult(new LlmResult(text, tokens));
    }

    public async IAsyncEnumerable<string> StreamGenerateAsync(string message, string persona, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var text = $"mock-reply:{persona}:{message}";
        foreach (var chunk in text.Split(':', StringSplitOptions.None))
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return chunk;
            await Task.Yield();
        }
    }

    private static int CountTokens(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return 1;
        }

        return message.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
    }
}
