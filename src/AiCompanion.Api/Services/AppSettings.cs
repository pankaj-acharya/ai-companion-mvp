using Microsoft.Extensions.Configuration;

namespace AiCompanion.Api.Services;

public sealed record AppSettings(
    string ApiKey,
    string Model,
    double Temperature,
    int MaxTokens,
    bool UseMockLlm,
    string ConnectionString)
{
    public static AppSettings FromConfiguration(IConfiguration configuration)
    {
        var useMockText = FirstNonEmpty(
            configuration["OpenAI:UseMock"],
            configuration["OPENAI_USE_MOCK"],
            "false");

        if (!bool.TryParse(useMockText, out var useMockLlm))
        {
            throw new InvalidOperationException("OpenAI use mock flag must be either true or false.");
        }

        var apiKey = FirstNonEmpty(
            configuration["OpenAI:ApiKey"],
            configuration["OPENAI_API_KEY"]);

        if (!useMockLlm && string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException("OPENAI_API_KEY or OpenAI:ApiKey must be configured.");
        }

        var model = FirstNonEmpty(
            configuration["OpenAI:Model"],
            configuration["OPENAI_MODEL"],
            "gpt-4o");

        var temperatureText = FirstNonEmpty(
            configuration["OpenAI:Temperature"],
            configuration["OPENAI_TEMPERATURE"],
            "0.3");

        var maxTokensText = FirstNonEmpty(
            configuration["OpenAI:MaxTokens"],
            configuration["OPENAI_MAX_TOKENS"],
            "512");

        var connectionString = GetConnectionString(configuration);

        if (!double.TryParse(temperatureText, out var temperature) || temperature < 0.0 || temperature > 2.0)
        {
            throw new InvalidOperationException("OpenAI temperature must be a number between 0.0 and 2.0.");
        }

        if (!int.TryParse(maxTokensText, out var maxTokens) || maxTokens <= 0 || maxTokens > 8192)
        {
            throw new InvalidOperationException("OpenAI max tokens must be between 1 and 8192.");
        }

        return new AppSettings(apiKey.Trim(), model, temperature, maxTokens, useMockLlm, connectionString);
    }

    public static string GetConnectionString(IConfiguration configuration)
    {
        return FirstNonEmpty(
            configuration.GetConnectionString("DefaultConnection"),
            configuration["ConnectionStrings:DefaultConnection"],
            "Data Source=chat.db");
    }

    private static string FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim() ?? string.Empty;
}