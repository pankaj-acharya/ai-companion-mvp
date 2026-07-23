using System.Net;
using System.Net.Http.Json;
using AiCompanion.Api.Contracts;

namespace AiCompanion.Api.Tests;

public sealed class MemoryApiTests(ApiWebApplicationFactory factory) : IClassFixture<ApiWebApplicationFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task ConsentDefaultsToFalseAndCanBeEnabled()
    {
        using var getRequest = new HttpRequestMessage(HttpMethod.Get, "/api/v1/memory/consent");
        getRequest.Headers.Add("Authorization", "Bearer memory-consent-user");

        using var getResponse = await _client.SendAsync(getRequest);
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        var defaultBody = await getResponse.Content.ReadFromJsonAsync<MemoryConsentResponse>();
        Assert.NotNull(defaultBody);
        Assert.False(defaultBody.Enabled);

        using var updateRequest = new HttpRequestMessage(HttpMethod.Put, "/api/v1/memory/consent")
        {
            Content = JsonContent.Create(new MemoryConsentRequest { Enabled = true }),
        };
        updateRequest.Headers.Add("Authorization", "Bearer memory-consent-user");

        using var updateResponse = await _client.SendAsync(updateRequest);
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        var updatedBody = await updateResponse.Content.ReadFromJsonAsync<MemoryConsentResponse>();
        Assert.NotNull(updatedBody);
        Assert.True(updatedBody.Enabled);
    }

    [Fact]
    public async Task CreateMemoryRequiresConsent()
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/memory")
        {
            Content = JsonContent.Create(new MemoryUpsertRequest
            {
                Scope = "long_term",
                Content = "Prefers short standups",
                IsApproved = true,
            }),
        };
        request.Headers.Add("Authorization", "Bearer memory-no-consent-user");

        using var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task MemoryCrudAndAuditTrail()
    {
        const string userId = "memory-crud-user";
        await EnableConsentAsync(userId);

        var created = await CreateMemoryAsync(userId, "long_term", "Likes action-oriented plans");
        Assert.True(created.Id > 0);
        Assert.True(created.IsApproved);

        using var listRequest = new HttpRequestMessage(HttpMethod.Get, "/api/v1/memory");
        listRequest.Headers.Add("Authorization", $"Bearer {userId}");
        using var listResponse = await _client.SendAsync(listRequest);
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        var listBody = await listResponse.Content.ReadFromJsonAsync<MemoryListResponse>();
        Assert.NotNull(listBody);
        Assert.Contains(listBody.Items, item => item.Id == created.Id);

        using var deleteRequest = new HttpRequestMessage(HttpMethod.Delete, $"/api/v1/memory/{created.Id}");
        deleteRequest.Headers.Add("Authorization", $"Bearer {userId}");
        using var deleteResponse = await _client.SendAsync(deleteRequest);
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        using var auditRequest = new HttpRequestMessage(HttpMethod.Get, "/api/v1/memory/audit?page=1&page_size=20");
        auditRequest.Headers.Add("Authorization", $"Bearer {userId}");
        using var auditResponse = await _client.SendAsync(auditRequest);
        Assert.Equal(HttpStatusCode.OK, auditResponse.StatusCode);
        var auditBody = await auditResponse.Content.ReadFromJsonAsync<MemoryAuditResponse>();
        Assert.NotNull(auditBody);
        Assert.Contains(auditBody.Events, item => item.Action == "consent_updated");
        Assert.Contains(auditBody.Events, item => item.Action == "memory_created");
        Assert.Contains(auditBody.Events, item => item.Action == "memory_deleted");
    }

    [Fact]
    public async Task ChatOrchestrationUsesApprovedMemory()
    {
        const string userId = "memory-orchestration-user";
        await EnableConsentAsync(userId);
        await CreateMemoryAsync(userId, "long_term", "Prefers concise bullet answers");

        using var chatRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/chat")
        {
            Content = JsonContent.Create(new ChatRequest
            {
                SessionId = "memory-s",
                Message = "How should you answer me?"
            }),
        };
        chatRequest.Headers.Add("Authorization", $"Bearer {userId}");

        using var chatResponse = await _client.SendAsync(chatRequest);
        Assert.Equal(HttpStatusCode.OK, chatResponse.StatusCode);

        var body = await chatResponse.Content.ReadFromJsonAsync<ChatResponse>();
        Assert.NotNull(body);
        Assert.Contains("Prefers concise bullet answers", body.Response, StringComparison.Ordinal);
    }

    private async Task EnableConsentAsync(string userId)
    {
        using var request = new HttpRequestMessage(HttpMethod.Put, "/api/v1/memory/consent")
        {
            Content = JsonContent.Create(new MemoryConsentRequest { Enabled = true }),
        };
        request.Headers.Add("Authorization", $"Bearer {userId}");

        using var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private async Task<MemoryItemResponse> CreateMemoryAsync(string userId, string scope, string content)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/memory")
        {
            Content = JsonContent.Create(new MemoryUpsertRequest
            {
                Scope = scope,
                Content = content,
                IsApproved = true,
            }),
        };
        request.Headers.Add("Authorization", $"Bearer {userId}");

        using var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<MemoryItemResponse>();
        Assert.NotNull(body);
        return body;
    }
}
