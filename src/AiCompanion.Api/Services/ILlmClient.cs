namespace AiCompanion.Api.Services;

public interface ILlmClient
{
    Task<LlmResult> GenerateAsync(string message, string persona, CancellationToken cancellationToken);

    IAsyncEnumerable<string> StreamGenerateAsync(string message, string persona, CancellationToken cancellationToken);
}

public sealed record LlmResult(string Text, int TokensUsed);

public static class LlmDefaults
{
    public const string DefaultPersona = "Supportive Friend";
}