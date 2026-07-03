using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AiCompanion.Api.Contracts;

namespace AiCompanion.Api.Tests;

public sealed class ChatApiTests(ApiWebApplicationFactory factory) : IClassFixture<ApiWebApplicationFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task RootEndpointReturnsApiInfo()
    {
        using var response = await _client.GetAsync("/");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(JsonValueKind.Object, body.ValueKind);
        Assert.Equal("AI Companion MVP API", body.GetProperty("name").GetString());
        Assert.Equal("/docs", body.GetProperty("docs").GetString());
        Assert.Equal("/health", body.GetProperty("health").GetString());
        Assert.True(body.GetProperty("mock_mode").GetBoolean());
    }

    [Fact]
    public async Task ChatEndpointHappyPath()
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/chat")
        {
            Content = JsonContent.Create(new ChatRequest { SessionId = "s1", Message = "hello" }),
        };
        request.Headers.Add("Authorization", "Bearer user-123");

        using var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<ChatResponse>();
        Assert.NotNull(body);
        Assert.Equal("s1", body.SessionId);
        Assert.Equal(11, body.TokensUsed);
        Assert.Equal($"reply:Supportive Friend:hello", body.Response);
    }

    [Fact]
    public async Task ChatEndpointReturns422OnInvalidPayload()
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/chat")
        {
            Content = JsonContent.Create(new ChatRequest { SessionId = string.Empty, Message = string.Empty }),
        };
        request.Headers.Add("Authorization", "Bearer user-123");

        using var response = await _client.SendAsync(request);
        Assert.Equal((HttpStatusCode)422, response.StatusCode);
    }

    [Fact]
    public async Task ChatEndpointRequiresAuth()
    {
        using var response = await _client.PostAsJsonAsync("/api/v1/chat", new ChatRequest { SessionId = "s1", Message = "hello" });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}