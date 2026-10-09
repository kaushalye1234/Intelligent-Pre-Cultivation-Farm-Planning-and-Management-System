using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using AgriAssist.Api.Data;
using AgriAssist.Api.Dtos.CropPlanning;
using AgriAssist.Api.Dtos.Shared;
using AgriAssist.Api.ExternalServices.AgenticAI;
using AgriAssist.Api.Models.Shared;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AgriAssist.Api.Tests;

public sealed class CropFindingAuthorizationIntegrationTests
{
    [Fact]
    public async Task CropFinding_http_client_uses_the_operation_specific_timeout()
    {
        await using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder => builder.UseEnvironment("Testing"));
        using var client = factory.Services
            .GetRequiredService<IHttpClientFactory>()
            .CreateClient(nameof(ICropFindingAIClient));
        var configuration = factory.Services.GetRequiredService<IConfiguration>();

        Assert.Equal(Timeout.InfiniteTimeSpan, client.Timeout);
        Assert.Equal(180, configuration.GetValue<int>("AI:CropFindingTimeoutSeconds"));
    }

    [Fact]
    public async Task CropFinding_is_admin_only_and_never_persists_suggestions()
    {
        await using var factory = CreateFactory();
        using var anonymous = factory.CreateClient();
        using var farmer = await ClientForAsync(factory, ApplicationRole.Farmer);
        using var admin = await ClientForAsync(factory, ApplicationRole.Admin);

        Assert.Equal(HttpStatusCode.Unauthorized,
            (await anonymous.PostAsJsonAsync("/api/crop-finding/suggest-crops", new SuggestCropsRequest())).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await farmer.PostAsJsonAsync("/api/crop-finding/suggest-crops", new SuggestCropsRequest())).StatusCode);

        var response = await admin.PostAsJsonAsync("/api/crop-finding/suggest-crops", new SuggestCropsRequest());
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<CropSuggestionsResponse>();
        Assert.Equal("Kurakkan", Assert.Single(result!.Suggestions).Name);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Empty(db.CropTypes);
    }

    private static async Task<HttpClient> ClientForAsync(WebApplicationFactory<Program> factory, ApplicationRole role)
    {
        var email = $"{role.ToString().ToLowerInvariant()}-{Guid.NewGuid():N}@cropfinding.test";
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Users.Add(new AppUser
            {
                FullName = $"CropFinding {role}",
                Email = email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("CropFinding@2026"),
                Role = role,
                IsActive = true,
                MustChangePassword = false,
                PasswordChangedAt = DateTime.UtcNow,
                TokenVersion = 1
            });
            await db.SaveChangesAsync();
        }

        var client = factory.CreateClient();
        var login = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, "CropFinding@2026"));
        var session = await login.Content.ReadFromJsonAsync<AuthResponse>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session!.AccessToken);
        return client;
    }

    private static WebApplicationFactory<Program> CreateFactory() =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Secret"] = "test-jwt-secret-with-at-least-32-chars",
                ["Jwt:Issuer"] = "AgriAssist",
                ["Jwt:Audience"] = "AgriAssistUsers",
                ["Jwt:ExpiryMinutes"] = "60"
            }));
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<ICropFindingAIClient>();
                services.AddSingleton<ICropFindingAIClient, StubCropFindingAIClient>();
            });
        });

    private sealed class StubCropFindingAIClient : ICropFindingAIClient
    {
        public Task<CropSuggestionsResponse> SuggestCropsAsync(SuggestCropsInput input, CancellationToken cancellationToken) =>
            Task.FromResult(new CropSuggestionsResponse(
                "request-1", "SuggestCrops", false, [],
                [new CropSuggestion("crop-1", "Kurakkan", "Finger millet", "Supported", "Listed by DOA.", [], [])],
                [], [], []));

        public Task<VarietySuggestionsResponse> SuggestVarietiesAsync(SuggestVarietiesInput input, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<ReferenceDiscoveryResponse> DiscoverReferencesAsync(DiscoverReferencesInput input, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }
}
