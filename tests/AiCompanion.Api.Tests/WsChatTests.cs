using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

namespace AiCompanion.Api.Tests;

public sealed class WsChatTests(ApiWebApplicationFactory factory) : IClassFixture<ApiWebApplicationFactory>
{
    [Fact]
    public async Task WebSocketStreamingHappyPath()
    {
        using var webSocket = await factory.Server.CreateWebSocketClient().ConnectAsync(new Uri("ws://localhost/ws/chat/ws-session?token=user-123"), CancellationToken.None);

        var payload = JsonSerializer.Serialize(new { message = "hello" });
        await webSocket.SendAsync(Encoding.UTF8.GetBytes(payload), WebSocketMessageType.Text, true, CancellationToken.None);

        var messages = new List<JsonDocument>();
<<<<<<< pankaj-acharya-epic-core-conversational-engine
        for (var index = 0; index < 4; index += 1)
        {
            messages.Add(await ReceiveJsonAsync(webSocket));
        }

        Assert.Equal("done", messages[^1].RootElement.GetProperty("type").GetString());
        Assert.True(messages[^1].RootElement.GetProperty("tokens_used").GetInt32() > 0);
        Assert.All(messages, document =>
        {
            var type = document.RootElement.GetProperty("type").GetString();
            Assert.Contains(type, new[] { "token", "done" });
        });
    }
=======
        try
        {
            for (var index = 0; index < 4; index += 1)
            {
                messages.Add(await ReceiveJsonAsync(webSocket));
            }

            Assert.Equal("done", messages[^1].RootElement.GetProperty("type").GetString());
            Assert.True(messages[^1].RootElement.GetProperty("tokens_used").GetInt32() > 0);
            Assert.All(messages, document =>
            {
                var type = document.RootElement.GetProperty("type").GetString();
                Assert.Contains(type, new[] { "token", "done" });
            });
        }
        finally
        {
            foreach (var document in messages)
            {
                document.Dispose();
            }
        }
>>>>>>> main

    [Fact]
    public async Task WebSocketRejectsMissingToken()
    {
        await Assert.ThrowsAnyAsync<Exception>(async () =>
        {
            using var webSocket = await factory.Server.CreateWebSocketClient().ConnectAsync(new Uri("ws://localhost/ws/chat/ws-session"), CancellationToken.None);
        });
    }

    private static async Task<JsonDocument> ReceiveJsonAsync(WebSocket webSocket)
    {
        var buffer = new byte[4096];
        using var payload = new MemoryStream();

        while (true)
        {
            var result = await webSocket.ReceiveAsync(buffer, CancellationToken.None);
            payload.Write(buffer, 0, result.Count);
            if (result.EndOfMessage)
            {
                break;
            }
        }

        return JsonDocument.Parse(payload.ToArray());
    }
}