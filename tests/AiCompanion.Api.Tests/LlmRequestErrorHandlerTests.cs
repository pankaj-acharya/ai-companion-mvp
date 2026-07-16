using System.Net;
using AiCompanion.Api.Endpoints;

namespace AiCompanion.Api.Tests;

public sealed class LlmRequestErrorHandlerTests
{
    [Fact]
    public void MapReturnsRateLimitResponseFor429()
    {
        var result = LlmRequestErrorHandler.Map(new HttpRequestException("rate limited", null, HttpStatusCode.TooManyRequests));

        Assert.Equal(429, result.statusCode);
        Assert.Equal("LLM provider rate limit reached. Please retry in a few seconds.", result.detail);
        Assert.True(result.isRateLimit);
    }

    [Fact]
    public void MapReturnsGatewayErrorForOtherHttpFailures()
    {
        var result = LlmRequestErrorHandler.Map(new HttpRequestException("bad gateway", null, HttpStatusCode.BadGateway));

        Assert.Equal(502, result.statusCode);
        Assert.Equal("LLM provider request failed. Please try again shortly.", result.detail);
        Assert.False(result.isRateLimit);
    }
}
