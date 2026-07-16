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
        var provider = GetSettingOrDefault(configuration, "openai", "LLM_PROVIDER", "Llm:Provider").ToLowerInvariant();

        if (provider is not ("openai" or "ollama"))
        {
            throw new InvalidOperationException("LLM provider must be either 'openai' or 'ollama'.");
        }

        var baseUrl = GetSettingOrDefault(
            configuration,
            provider == "ollama" ? "http://localhost:11434/v1/" : "https://api.openai.com/v1/",
            "LLM_BASE_URL",
            "OPENAI_BASE_URL",
            "Llm:BaseUrl",
            "OpenAI:BaseUrl");

        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out _))
        {
            throw new InvalidOperationException("LLM base URL must be a valid absolute URL.");
        }

        var useMockText = GetSettingOrDefault(configuration, "false", "OPENAI_USE_MOCK", "OpenAI:UseMock");

        if (!bool.TryParse(useMockText, out var useMockLlm))
        {
            throw new InvalidOperationException("OpenAI use mock flag must be either true or false.");
        }

        var apiKey = GetSetting(configuration, "OPENAI_API_KEY", "OpenAI:ApiKey");
        var apiKeyRequired = !useMockLlm && provider != "ollama";
        if (apiKeyRequired && string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException("OPENAI_API_KEY or OpenAI:ApiKey must be configured.");
        }

        var model = GetSettingOrDefault(configuration, "gpt-4o", "OPENAI_MODEL", "OpenAI:Model");
        var temperatureText = GetSettingOrDefault(configuration, "0.3", "OPENAI_TEMPERATURE", "OpenAI:Temperature");
        var maxTokensText = GetSettingOrDefault(configuration, "512", "OPENAI_MAX_TOKENS", "OpenAI:MaxTokens");
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

    private static string GetSetting(IConfiguration configuration, params string[] keys)
    {
        return FirstNonEmpty(keys.Select(key => configuration[key]).ToArray());
    }

    private static string GetSettingOrDefault(IConfiguration configuration, string defaultValue, params string[] keys)
    {
        return FirstNonEmpty(keys.Select(key => configuration[key]).Concat([defaultValue]).ToArray());
    }

    private static string FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim() ?? string.Empty;
}
