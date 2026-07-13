using Microsoft.Extensions.Configuration;

namespace AiCompanion.Api.Services;

public sealed record AppSettings(
    string ApiKey,
    string Model,
    double Temperature,
    int MaxTokens,
<<<<<<< pankaj-acharya-epic-core-conversational-engine
    bool UseMockLlm,
=======
>>>>>>> main
    string ConnectionString)
{
    public static AppSettings FromConfiguration(IConfiguration configuration)
    {
<<<<<<< pankaj-acharya-epic-core-conversational-engine
        var useMockText = FirstNonEmpty(
            configuration["OpenAI:UseMock"],
            configuration["OPENAI_USE_MOCK"],
            "false");

        if (!bool.TryParse(useMockText, out var useMockLlm))
        {
            throw new InvalidOperationException("OpenAI use mock flag must be either true or false.");
        }

=======
>>>>>>> main
        var apiKey = FirstNonEmpty(
            configuration["OpenAI:ApiKey"],
            configuration["OPENAI_API_KEY"]);

<<<<<<< pankaj-acharya-epic-core-conversational-engine
        if (!useMockLlm && string.IsNullOrWhiteSpace(apiKey))
=======
        if (string.IsNullOrWhiteSpace(apiKey))
>>>>>>> main
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

<<<<<<< pankaj-acharya-epic-core-conversational-engine
        if (!double.TryParse(temperatureText, out var temperature) || temperature < 0.0 || temperature > 2.0)
=======
        if (!double.TryParse(temperatureText, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var temperature) || temperature < 0.0 || temperature > 2.0)
>>>>>>> main
        {
            throw new InvalidOperationException("OpenAI temperature must be a number between 0.0 and 2.0.");
        }

        if (!int.TryParse(maxTokensText, out var maxTokens) || maxTokens <= 0 || maxTokens > 8192)
        {
            throw new InvalidOperationException("OpenAI max tokens must be between 1 and 8192.");
        }

<<<<<<< pankaj-acharya-epic-core-conversational-engine
        return new AppSettings(apiKey.Trim(), model, temperature, maxTokens, useMockLlm, connectionString);
=======
        return new AppSettings(apiKey.Trim(), model, temperature, maxTokens, connectionString);
>>>>>>> main
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