using Microsoft.Extensions.Configuration;

namespace AiCompanion.Api.Services;

public sealed record AppSettings(
    string Provider,
    string BaseUrl,
    string ApiKey,
    string Model,
    double Temperature,
    int MaxTokens,
    bool UseMockLlm,
    string ConnectionString)
{
    public static AppSettings FromConfiguration(IConfiguration configuration)
    {
        var provider = FirstNonEmpty(
            configuration["LLM_PROVIDER"],
            configuration["Llm:Provider"],
            "openai").ToLowerInvariant();

        if (provider is not ("openai" or "ollama"))
        {
            throw new InvalidOperationException("LLM provider must be either 'openai' or 'ollama'.");
        }

        var baseUrl = FirstNonEmpty(
            configuration["LLM_BASE_URL"],
            configuration["OPENAI_BASE_URL"],
            configuration["Llm:BaseUrl"],
            configuration["OpenAI:BaseUrl"],
            provider == "ollama" ? "http://localhost:11434/v1/" : "https://api.openai.com/v1/");

        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out _))
        {
            throw new InvalidOperationException("LLM base URL must be a valid absolute URL.");
        }

        var useMockText = FirstNonEmpty(
            configuration["OPENAI_USE_MOCK"],
            configuration["OpenAI:UseMock"],
            "false");

        if (!bool.TryParse(useMockText, out var useMockLlm))
        {
            throw new InvalidOperationException("OpenAI use mock flag must be either true or false.");
        }
        var apiKey = FirstNonEmpty(
            configuration["OPENAI_API_KEY"],
            configuration["OpenAI:ApiKey"]);

        var apiKeyRequired = !useMockLlm && provider != "ollama";
        if (apiKeyRequired && string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException("OPENAI_API_KEY or OpenAI:ApiKey must be configured.");
        }

        var model = FirstNonEmpty(
            configuration["OPENAI_MODEL"],
            configuration["OpenAI:Model"],
            "gpt-4o");

        var temperatureText = FirstNonEmpty(
            configuration["OPENAI_TEMPERATURE"],
            configuration["OpenAI:Temperature"],
            "0.3");

        var maxTokensText = FirstNonEmpty(
            configuration["OPENAI_MAX_TOKENS"],
            configuration["OpenAI:MaxTokens"],
            "512");

        var connectionString = GetConnectionString(configuration);

        if (!double.TryParse(temperatureText, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var temperature) || temperature < 0.0 || temperature > 2.0)
        {
            throw new InvalidOperationException("OpenAI temperature must be a number between 0.0 and 2.0.");
        }

        if (!int.TryParse(maxTokensText, out var maxTokens) || maxTokens <= 0 || maxTokens > 8192)
        {
            throw new InvalidOperationException("OpenAI max tokens must be between 1 and 8192.");
        }

        return new AppSettings(provider, baseUrl.Trim(), apiKey.Trim(), model, temperature, maxTokens, useMockLlm, connectionString);
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