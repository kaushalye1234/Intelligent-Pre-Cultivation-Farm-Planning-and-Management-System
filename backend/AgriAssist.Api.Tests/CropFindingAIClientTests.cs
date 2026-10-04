using System.Net;
using System.Net.Http.Json;
using System.Text;
using AgriAssist.Api.Dtos.CropPlanning;
using AgriAssist.Api.ExternalServices.AgenticAI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgriAssist.Api.Tests;

public sealed class CropFindingAIClientTests
{
    [Theory]
    [InlineData(HttpStatusCode.BadGateway)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    [InlineData(HttpStatusCode.GatewayTimeout)]
    public async Task CropFinding_non_success_response_preserves_structured_error(HttpStatusCode statusCode)
    {
        using var httpClient = new HttpClient(new StubHandler(statusCode, JsonContent.Create(new
        {
            detail = new
            {
                code = "CROP_FINDING_PROVIDER_FAILURE",
                message = "OpenAI returned a server error.",
                requestId = "request-1",
                operation = "structured_analysis",
                stage = 2,
                attempt = 1,
                category = "server_error",
                upstreamStatus = 503,
                providerErrorCode = "server_overloaded",
                providerRequestId = "provider-request-1",
                configuredTimeoutSeconds = 45,
                effectiveTimeoutSeconds = 40,
            }
        })));
        var client = NewClient(httpClient);

        var error = await Assert.ThrowsAsync<CropFindingAIException>(() =>
            client.DiscoverReferencesAsync(Input(), CancellationToken.None));

        Assert.Equal(statusCode, error.ResponseStatusCode);
        Assert.Equal("CROP_FINDING_PROVIDER_FAILURE", error.Detail!.Code);
        Assert.Equal("structured_analysis", error.Detail.Operation);
        Assert.Equal(2, error.Detail.Stage);
        Assert.Equal("server_overloaded", error.Detail.ProviderErrorCode);
        Assert.Equal(40, error.Detail.EffectiveTimeoutSeconds);
    }

    [Fact]
    public async Task CropFinding_malformed_error_body_keeps_status_without_exposing_body()
    {
        const string rawBody = "RAW_UPSTREAM_SECRET";
        using var content = new StringContent(rawBody, Encoding.UTF8, "application/json");
        using var httpClient = new HttpClient(new StubHandler(HttpStatusCode.BadGateway, content));
        var client = NewClient(httpClient);

        var error = await Assert.ThrowsAsync<CropFindingAIException>(() =>
            client.DiscoverReferencesAsync(Input(), CancellationToken.None));

        Assert.Equal(HttpStatusCode.BadGateway, error.ResponseStatusCode);
        Assert.Null(error.Detail);
        Assert.DoesNotContain(rawBody, error.Message, StringComparison.Ordinal);
    }

    private static AgenticAIClient NewClient(HttpClient httpClient)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["AI:ServiceUrl"] = "http://ai.test",
            ["AI:ServiceToken"] = "test-token",
            ["AI:CropFindingTimeoutSeconds"] = "180",
        }).Build();
        return new AgenticAIClient(httpClient, configuration, NullLogger<AgenticAIClient>.Instance);
    }

    private static DiscoverReferencesInput Input() =>
        new(Guid.NewGuid(), Guid.NewGuid(), "Rice", null, null, "Sri Lanka");

    private sealed class StubHandler(HttpStatusCode statusCode, HttpContent content) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(statusCode) { Content = content });
    }
}
