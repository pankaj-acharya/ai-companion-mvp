using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AiCompanion.Api.Services;

public sealed class OpenAiLlmClient(HttpClient httpClient, AppSettings settings) : ILlmClient
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public async Task<LlmResult> GenerateAsync(string message, string persona, CancellationToken cancellationToken)
    {
        using var request = BuildRequest(stream: false, message, persona);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var payload = await JsonSerializer.DeserializeAsync<OpenAiChatResponse>(stream, SerializerOptions, cancellationToken)
            ?? throw new InvalidOperationException("OpenAI returned an empty response.");

        var text = payload.Choices.FirstOrDefault()?.Message?.Content ?? string.Empty;
        return new LlmResult(text, payload.Usage?.TotalTokens ?? 0);
    }

    public async IAsyncEnumerable<string> StreamGenerateAsync(string message, string persona, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        using var request = BuildRequest(stream: true, message, persona);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));

        using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(responseStream, Encoding.UTF8);

        while (!reader.EndOfStream)
        {
            var line = await reader.ReadLineAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(line) || !line.StartsWith("data:", StringComparison.Ordinal))
            {
                continue;
            }

            var payload = line[5..].Trim();
            if (string.Equals(payload, "[DONE]", StringComparison.Ordinal))
            {
                yield break;
            }

            var chunk = JsonSerializer.Deserialize<OpenAiStreamChunk>(payload, SerializerOptions);
            var delta = chunk?.Choices.FirstOrDefault()?.Delta?.Content;
            if (!string.IsNullOrEmpty(delta))
            {
                yield return delta;
            }
        }
    }

    private HttpRequestMessage BuildRequest(bool stream, string message, string persona)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "chat/completions");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", settings.ApiKey);
        request.Content = JsonContent.Create(new OpenAiChatRequest
        {
            Model = settings.Model,
            Temperature = settings.Temperature,
            MaxTokens = settings.MaxTokens,
            Stream = stream,
            Messages =
            [
                new OpenAiChatMessage("system", $"You are the '{persona}' persona for an AI companion."),
                new OpenAiChatMessage("user", message),
            ],
        });
        return request;
    }

    private sealed class OpenAiChatRequest
    {
        [JsonPropertyName("model")]
        public string Model { get; init; } = string.Empty;

        [JsonPropertyName("temperature")]
        public double Temperature { get; init; }

        [JsonPropertyName("max_tokens")]
        public int MaxTokens { get; init; }

        [JsonPropertyName("stream")]
        public bool Stream { get; init; }

        [JsonPropertyName("messages")]
        public IReadOnlyList<OpenAiChatMessage> Messages { get; init; } = [];
    }

    private sealed record OpenAiChatMessage(
        [property: JsonPropertyName("role")] string Role,
        [property: JsonPropertyName("content")] string Content);

    private sealed class OpenAiChatResponse
    {
        [JsonPropertyName("choices")]
        public List<OpenAiChoice> Choices { get; init; } = [];

        [JsonPropertyName("usage")]
        public OpenAiUsage? Usage { get; init; }
    }

    private sealed class OpenAiChoice
    {
        [JsonPropertyName("message")]
        public OpenAiMessage? Message { get; init; }
    }

    private sealed class OpenAiMessage
    {
        [JsonPropertyName("content")]
        public string? Content { get; init; }
    }

    private sealed class OpenAiUsage
    {
        [JsonPropertyName("total_tokens")]
        public int TotalTokens { get; init; }
    }

    private sealed class OpenAiStreamChunk
    {
        [JsonPropertyName("choices")]
        public List<OpenAiStreamChoice> Choices { get; init; } = [];
    }

    private sealed class OpenAiStreamChoice
    {
        [JsonPropertyName("delta")]
        public OpenAiDelta? Delta { get; init; }
    }

    private sealed class OpenAiDelta
    {
        [JsonPropertyName("content")]
        public string? Content { get; init; }
    }
}