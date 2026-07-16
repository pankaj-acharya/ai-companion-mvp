using System.Net;
using System.Text;
using AiCompanion.Api.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace AiCompanion.Api.Tests;

public sealed class OpenAiLlmClientTests
{
    [Fact]
    public async Task GenerateAsyncUsesResponsesApiForGpt5Models()
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""
            {
              "output_text": "hello from gpt-5",
              "usage": {
                "total_tokens": 42
              }
            }
            """, Encoding.UTF8, "application/json")
        });
        using var client = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.openai.com/v1/")
        };
        var settings = new AppSettings("openai", "https://api.openai.com/v1/", "test-key", "gpt-5-nano", 0.3, 512, false, "Data Source=test.db");
        var sut = new OpenAiLlmClient(client, settings, NullLogger<OpenAiLlmClient>.Instance);

        var result = await sut.GenerateAsync("hello", "Supportive Friend", CancellationToken.None);

        Assert.Equal("https://api.openai.com/v1/responses", handler.Requests.Single().RequestUri?.ToString());
        var requestBody = handler.RequestBodies.Single();
        Assert.DoesNotContain("\"temperature\"", requestBody, StringComparison.Ordinal);
        Assert.Equal("hello from gpt-5", result.Text);
        Assert.Equal(42, result.TokensUsed);
    }

    [Fact]
    public async Task GenerateAsyncUsesChatCompletionsForLegacyModels()
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""
            {
              "choices": [
                {
                  "message": {
                    "content": "hello from gpt-4o"
                  }
                }
              ],
              "usage": {
                "total_tokens": 11
              }
            }
            """, Encoding.UTF8, "application/json")
        });
        using var client = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.openai.com/v1/")
        };
        var settings = new AppSettings("openai", "https://api.openai.com/v1/", "test-key", "gpt-4o", 0.3, 512, false, "Data Source=test.db");
        var sut = new OpenAiLlmClient(client, settings, NullLogger<OpenAiLlmClient>.Instance);

        var result = await sut.GenerateAsync("hello", "Supportive Friend", CancellationToken.None);

        Assert.Equal("https://api.openai.com/v1/chat/completions", handler.Requests.Single().RequestUri?.ToString());
        Assert.Equal("hello from gpt-4o", result.Text);
        Assert.Equal(11, result.TokensUsed);
    }

    [Fact]
    public async Task GenerateAsyncExtractsTextWhenChatCompletionContentIsArray()
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""
            {
              "choices": [
                {
                  "message": {
                    "content": [
                      {
                        "type": "output_text",
                        "text": "hello from array content"
                      }
                    ]
                  }
                }
              ],
              "usage": {
                "total_tokens": 19
              }
            }
            """, Encoding.UTF8, "application/json")
        });
        using var client = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.openai.com/v1/")
        };
        var settings = new AppSettings("openai", "https://api.openai.com/v1/", "test-key", "gpt-4o", 0.3, 512, false, "Data Source=test.db");
        var sut = new OpenAiLlmClient(client, settings, NullLogger<OpenAiLlmClient>.Instance);

        var result = await sut.GenerateAsync("hello", "Supportive Friend", CancellationToken.None);

        Assert.Equal("hello from array content", result.Text);
        Assert.Equal(19, result.TokensUsed);
    }

    [Fact]
    public async Task GenerateAsyncExtractsRefusalTextFromResponsesApi()
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""
            {
              "output": [
                {
                  "role": "assistant",
                  "content": [
                    {
                      "type": "refusal",
                      "refusal": "I cannot help with that request."
                    }
                  ]
                }
              ],
              "usage": {
                "total_tokens": 27
              }
            }
            """, Encoding.UTF8, "application/json")
        });
        using var client = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.openai.com/v1/")
        };
        var settings = new AppSettings("openai", "https://api.openai.com/v1/", "test-key", "gpt-5-mini", 0.3, 512, false, "Data Source=test.db");
        var sut = new OpenAiLlmClient(client, settings, NullLogger<OpenAiLlmClient>.Instance);

        var result = await sut.GenerateAsync("hello", "Supportive Friend", CancellationToken.None);

        Assert.Equal("I cannot help with that request.", result.Text);
        Assert.Equal(27, result.TokensUsed);
    }

    [Fact]
    public async Task GenerateAsyncIncludesUpstreamErrorMessageInException()
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            Content = new StringContent("""
            {
              "error": {
                "message": "Unsupported parameter: 'max_tokens'."
              }
            }
            """, Encoding.UTF8, "application/json")
        });
        using var client = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.openai.com/v1/")
        };
        var settings = new AppSettings("openai", "https://api.openai.com/v1/", "test-key", "gpt-4o", 0.3, 512, false, "Data Source=test.db");
        var sut = new OpenAiLlmClient(client, settings, NullLogger<OpenAiLlmClient>.Instance);

        var exception = await Assert.ThrowsAsync<HttpRequestException>(() => sut.GenerateAsync("hello", "Supportive Friend", CancellationToken.None));

        Assert.Contains("Unsupported parameter: 'max_tokens'.", exception.Message, StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.BadRequest, exception.StatusCode);
    }

    private sealed class StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responseFactory) : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = [];
      public List<string> RequestBodies { get; } = [];

      protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
        RequestBodies.Add(request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken));
        return responseFactory(request);
        }
    }
}