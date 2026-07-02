using System.Net;
using System.Net.Http.Json;
using AiCompanion.Api.Contracts;

namespace AiCompanion.Api.Tests;

public sealed class HistoryApiTests(ApiWebApplicationFactory factory) : IClassFixture<ApiWebApplicationFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task HistoryReturnsPaginatedMessages()
    {
        await SendChat("history-s", "first", "user-123");
        await SendChat("history-s", "second", "user-123");

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/chat/history/history-s?page=1&page_size=2");
        request.Headers.Add("Authorization", "Bearer user-123");

        using var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<HistoryResponse>();
        Assert.NotNull(body);
        Assert.Equal(4, body.Total);
        Assert.Equal(1, body.Page);
        Assert.Equal(2, body.PageSize);
        Assert.Equal(2, body.Messages.Count);
    }

    [Fact]
    public async Task HistoryReturns404WhenSessionIsMissing()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/chat/history/does-not-exist");
        request.Headers.Add("Authorization", "Bearer user-123");

        using var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task HistoryIsUserScoped()
    {
        await SendChat("private-s", "secret", "user-123");

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/chat/history/private-s");
        request.Headers.Add("Authorization", "Bearer another-user");

        using var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private async Task SendChat(string sessionId, string message, string userId)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/chat")
        {
            Content = JsonContent.Create(new ChatRequest { SessionId = sessionId, Message = message }),
        };
        request.Headers.Add("Authorization", $"Bearer {userId}");

        using var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}