using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace AiCompanion.Api.Services;

public sealed class OpenAiLlmClient(HttpClient httpClient, AppSettings settings, ILogger<OpenAiLlmClient> logger) : ILlmClient
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public async Task<LlmResult> GenerateAsync(string message, string persona, CancellationToken cancellationToken, string? modelId = null)
    {
        if (UseResponsesApi(modelId))
        {
            return await GenerateWithResponsesApiAsync(message, persona, cancellationToken, modelId);
        }

        using var request = BuildRequest(stream: false, message, persona, modelId);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessStatusCodeAsync(response, cancellationToken);

        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        LogRawJsonResponse("OpenAI chat completions raw response", responseBody);

        var payload = JsonSerializer.Deserialize<OpenAiChatResponse>(responseBody, SerializerOptions)
            ?? throw new InvalidOperationException("OpenAI returned an empty response.");

        var text = ExtractChatCompletionText(payload);
        WarnIfEmptyAssistantText("chat/completions", text, responseBody, modelId);
        return new LlmResult(text, payload.Usage?.TotalTokens ?? 0);
    }

    public async IAsyncEnumerable<string> StreamGenerateAsync(string message, string persona, [EnumeratorCancellation] CancellationToken cancellationToken, string? modelId = null)
    {
        if (UseResponsesApi(modelId))
        {
            var result = await GenerateWithResponsesApiAsync(message, persona, cancellationToken, modelId);
            if (!string.IsNullOrEmpty(result.Text))
            {
                yield return result.Text;
            }

            yield break;
        }

        using var request = BuildRequest(stream: true, message, persona, modelId);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));

        using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        await EnsureSuccessStatusCodeAsync(response, cancellationToken);

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

    private async Task<LlmResult> GenerateWithResponsesApiAsync(string message, string persona, CancellationToken cancellationToken, string? modelId)
    {
        using var request = BuildResponsesRequest(message, persona, modelId);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessStatusCodeAsync(response, cancellationToken);

        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        LogRawJsonResponse("OpenAI responses raw response", responseBody);

        var payload = JsonSerializer.Deserialize<OpenAiResponseApiResponse>(responseBody, SerializerOptions)
            ?? throw new InvalidOperationException("OpenAI returned an empty response.");

        var text = ExtractResponsesText(payload);

        WarnIfEmptyAssistantText("responses", text, responseBody, modelId);

        return new LlmResult(text, payload.Usage?.TotalTokens ?? 0);
    }

    private void LogRawJsonResponse(string message, string responseBody)
    {
        logger.LogInformation("{Message}:{NewLine}{ResponseBody}", message, Environment.NewLine, FormatJsonForLogging(responseBody));
    }

    private static string FormatJsonForLogging(string responseBody)
    {
        if (string.IsNullOrWhiteSpace(responseBody))
        {
            return "<empty>";
        }

        try
        {
            using var document = JsonDocument.Parse(responseBody);
            return JsonSerializer.Serialize(document.RootElement, new JsonSerializerOptions
            {
                WriteIndented = true
            });
        }
        catch (JsonException)
        {
            return responseBody;
        }
    }

    private void WarnIfEmptyAssistantText(string endpoint, string text, string responseBody, string? modelId)
    {
        if (!string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        logger.LogWarning(
            "OpenAI {Endpoint} returned HTTP 200 but no assistant text was extracted. Model: {Model}. Raw payload:{NewLine}{ResponseBody}",
            endpoint,
            ResolveModel(modelId),
            Environment.NewLine,
            FormatJsonForLogging(responseBody));
    }

    private HttpRequestMessage BuildRequest(bool stream, string message, string persona, string? modelId)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "chat/completions");
        if (!string.IsNullOrWhiteSpace(settings.ApiKey))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", settings.ApiKey);
        }
        request.Content = JsonContent.Create(new OpenAiChatRequest
        {
            Model = ResolveModel(modelId),
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

    private static string ExtractChatCompletionText(OpenAiChatResponse payload)
    {
        foreach (var choice in payload.Choices)
        {
            var text = ExtractChatMessageText(choice.Message);
            if (!string.IsNullOrWhiteSpace(text))
            {
                return text;
            }
        }

        return string.Empty;
    }

    private static string ExtractChatMessageText(OpenAiMessage? message)
    {
        if (message?.Content is null)
        {
            return string.Empty;
        }

        var content = message.Content.Value;
        if (content.ValueKind == JsonValueKind.String)
        {
            return content.GetString() ?? string.Empty;
        }

        if (content.ValueKind != JsonValueKind.Array)
        {
            return string.Empty;
        }

        foreach (var item in content.EnumerateArray())
        {
            var text = ExtractTextValue(item, "text");
            if (!string.IsNullOrWhiteSpace(text))
            {
                return text;
            }
        }

        return string.Empty;
    }

    private static string ExtractResponsesText(OpenAiResponseApiResponse payload)
    {
        if (!string.IsNullOrWhiteSpace(payload.OutputText))
        {
            return payload.OutputText;
        }

        if (payload.Output is null)
        {
            return string.Empty;
        }

        foreach (var item in payload.Output.Where(item => string.Equals(item.Role, "assistant", StringComparison.OrdinalIgnoreCase)))
        {
            foreach (var content in item.Content ?? [])
            {
                var text = ExtractResponseContentText(content);
                if (!string.IsNullOrWhiteSpace(text))
                {
                    return text;
                }
            }
        }

        return string.Empty;
    }

    private static string ExtractResponseContentText(OpenAiResponseOutputContent content)
    {
        var text = ExtractTextValue(content.Text, "value");
        if (!string.IsNullOrWhiteSpace(text))
        {
            return text;
        }

        if (!string.IsNullOrWhiteSpace(content.Refusal))
        {
            return content.Refusal;
        }

        return string.Empty;
    }

    private static string ExtractTextValue(JsonElement? value, string nestedField)
    {
        if (value is null)
        {
            return string.Empty;
        }

        var content = value.Value;
        if (content.ValueKind == JsonValueKind.String)
        {
            return content.GetString() ?? string.Empty;
        }

        if (content.ValueKind == JsonValueKind.Object
            && content.TryGetProperty(nestedField, out var nested)
            && nested.ValueKind == JsonValueKind.String)
        {
            return nested.GetString() ?? string.Empty;
        }

        return string.Empty;
    }

    private HttpRequestMessage BuildResponsesRequest(string message, string persona, string? modelId)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "responses");
        if (!string.IsNullOrWhiteSpace(settings.ApiKey))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", settings.ApiKey);
        }

        request.Content = JsonContent.Create(new OpenAiResponsesRequest
        {
            Model = ResolveModel(modelId),
            Instructions = $"You are the '{persona}' persona for an AI companion.",
            Input = message,
            MaxOutputTokens = settings.MaxTokens,
        });
        return request;
    }

    private bool UseResponsesApi(string? modelId)
    {
        var effectiveModel = ResolveModel(modelId);
        return string.Equals(settings.Provider, "openai", StringComparison.OrdinalIgnoreCase)
            && effectiveModel.StartsWith("gpt-5", StringComparison.OrdinalIgnoreCase);
    }

    private string ResolveModel(string? modelId)
    {
        return string.IsNullOrWhiteSpace(modelId) ? settings.Model : modelId.Trim();
    }

    private static async Task EnsureSuccessStatusCodeAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var responseBody = response.Content is null
            ? string.Empty
            : await response.Content.ReadAsStringAsync(cancellationToken);
        var detail = TryExtractErrorMessage(responseBody);
        var message = string.IsNullOrWhiteSpace(detail)
            ? $"Response status code does not indicate success: {(int)response.StatusCode} ({response.ReasonPhrase})."
            : $"Response status code does not indicate success: {(int)response.StatusCode} ({response.ReasonPhrase}). {detail}";

        throw new HttpRequestException(message, null, response.StatusCode);
    }

    private static string? TryExtractErrorMessage(string? responseBody)
    {
        if (string.IsNullOrWhiteSpace(responseBody))
        {
            return null;
        }

        try
        {
            var payload = JsonSerializer.Deserialize<OpenAiErrorEnvelope>(responseBody, SerializerOptions);
            return payload?.Error?.Message;
        }
        catch (JsonException)
        {
            return responseBody;
        }
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

    private sealed class OpenAiResponsesRequest
    {
        [JsonPropertyName("model")]
        public string Model { get; init; } = string.Empty;

        [JsonPropertyName("instructions")]
        public string Instructions { get; init; } = string.Empty;

        [JsonPropertyName("input")]
        public string Input { get; init; } = string.Empty;

        [JsonPropertyName("max_output_tokens")]
        public int MaxOutputTokens { get; init; }
    }

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
        public JsonElement? Content { get; init; }
    }

    private sealed class OpenAiUsage
    {
        [JsonPropertyName("total_tokens")]
        public int TotalTokens { get; init; }
    }

    private sealed class OpenAiResponseApiResponse
    {
        [JsonPropertyName("output_text")]
        public string? OutputText { get; init; }

        [JsonPropertyName("output")]
        public List<OpenAiResponseOutputItem>? Output { get; init; }

        [JsonPropertyName("usage")]
        public OpenAiUsage? Usage { get; init; }
    }

    private sealed class OpenAiResponseOutputItem
    {
        [JsonPropertyName("role")]
        public string? Role { get; init; }

        [JsonPropertyName("content")]
        public List<OpenAiResponseOutputContent>? Content { get; init; }
    }

    private sealed class OpenAiResponseOutputContent
    {
        [JsonPropertyName("type")]
        public string? Type { get; init; }

        [JsonPropertyName("text")]
        public JsonElement? Text { get; init; }

        [JsonPropertyName("refusal")]
        public string? Refusal { get; init; }
    }

    private sealed class OpenAiErrorEnvelope
    {
        [JsonPropertyName("error")]
        public OpenAiError? Error { get; init; }
    }

    private sealed class OpenAiError
    {
        [JsonPropertyName("message")]
        public string? Message { get; init; }
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
