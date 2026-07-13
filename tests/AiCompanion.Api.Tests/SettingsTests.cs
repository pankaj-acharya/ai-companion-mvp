using AiCompanion.Api.Services;
using Microsoft.Extensions.Configuration;

namespace AiCompanion.Api.Tests;

public sealed class SettingsTests
{
    [Fact]
    public void SettingsReadsConfiguration()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["OpenAI:ApiKey"] = "k",
                ["OpenAI:Model"] = "gpt-4o-mini",
                ["OpenAI:Temperature"] = "0.5",
                ["OpenAI:MaxTokens"] = "700",
                ["ConnectionStrings:DefaultConnection"] = "Data Source=test.db",
            })
            .Build();

        var settings = AppSettings.FromConfiguration(configuration);
        Assert.Equal("gpt-4o-mini", settings.Model);
        Assert.Equal(0.5, settings.Temperature);
        Assert.Equal(700, settings.MaxTokens);
<<<<<<< pankaj-acharya-epic-core-conversational-engine
        Assert.False(settings.UseMockLlm);
=======
>>>>>>> main
    }

    [Fact]
    public void SettingsFailsWhenApiKeyIsMissing()
    {
        var configuration = new ConfigurationBuilder().Build();
        Assert.Throws<InvalidOperationException>(() => AppSettings.FromConfiguration(configuration));
    }
<<<<<<< pankaj-acharya-epic-core-conversational-engine

    [Fact]
    public void SettingsAllowMissingApiKeyInMockMode()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["OpenAI:UseMock"] = "true",
            })
            .Build();

        var settings = AppSettings.FromConfiguration(configuration);
        Assert.True(settings.UseMockLlm);
    }
=======
>>>>>>> main
}