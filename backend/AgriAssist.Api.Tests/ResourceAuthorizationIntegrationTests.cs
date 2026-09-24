using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using AgriAssist.Api.Data;
using AgriAssist.Api.Dtos.Resources;
using AgriAssist.Api.Dtos.Shared;
using AgriAssist.Api.Models.Shared;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AgriAssist.Api.Tests;

/// <summary>HTTP-level checks that resource writes stay limited to ResourceOfficer/Admin and reads stay open.</summary>
public sealed class ResourceAuthorizationIntegrationTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task Farmer_can_read_resources_but_cannot_create_update_or_delete_anything()
    {
        await using var factory = CreateFactory();
        using var officer = await ClientForAsync(factory, ApplicationRole.ResourceOfficer);
        using var farmer = await ClientForAsync(factory, ApplicationRole.Farmer);

        var category = await CreateAsync<ResourceCategoryResponse>(officer, "/api/resources/categories", new ResourceCategoryRequest("Seeds", null));
        var supplier = await CreateAsync<SupplierResponse>(officer, "/api/resources/suppliers", new SupplierRequest("Acme", "acme@test.example", "1"));
        var resource = await CreateAsync<ResourceResponse>(officer, "/api/resources", new ResourceRequest(category.Id, supplier.Id, "Paddy Seed", "kg", true));

        Assert.Equal(HttpStatusCode.OK, (await farmer.GetAsync("/api/resources")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await farmer.GetAsync($"/api/resources?categoryId={category.Id}&supplierId={supplier.Id}&sortBy=name&sortDirection=desc")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await farmer.GetAsync("/api/resources/stocks")).StatusCode);

        var resourceBody = new ResourceRequest(category.Id, supplier.Id, "Hacked", "kg", true);
        Assert.Equal(HttpStatusCode.Forbidden, (await farmer.PostAsJsonAsync("/api/resources", resourceBody)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await farmer.PutAsJsonAsync($"/api/resources/{resource.Id}", resourceBody)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await farmer.DeleteAsync($"/api/resources/{resource.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await farmer.PutAsJsonAsync($"/api/resources/categories/{category.Id}", new ResourceCategoryRequest("Hacked", null))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await farmer.DeleteAsync($"/api/resources/categories/{category.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await farmer.PutAsJsonAsync($"/api/resources/suppliers/{supplier.Id}", new SupplierRequest("Hacked", "h@test.example", "1"))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await farmer.DeleteAsync($"/api/resources/suppliers/{supplier.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await farmer.PostAsJsonAsync("/api/resources/stocks", new InventoryStockRequest(resource.Id, 5, 1))).StatusCode);

        // Nothing changed.
        var stored = await officer.GetFromJsonAsync<PagedResult<ResourceResponse>>("/api/resources", Json);
        Assert.Equal("Paddy Seed", Assert.Single(stored!.Items).Name);
    }

    [Fact]
    public async Task Anonymous_callers_are_rejected()
    {
        await using var factory = CreateFactory();
        using var anonymous = factory.CreateClient();

        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/resources")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.DeleteAsync($"/api/resources/{Guid.NewGuid()}")).StatusCode);
    }

    [Fact]
    public async Task Resource_officer_and_admin_can_update_and_delete_with_proper_status_codes()
    {
        await using var factory = CreateFactory();
        using var officer = await ClientForAsync(factory, ApplicationRole.ResourceOfficer);
        using var admin = await ClientForAsync(factory, ApplicationRole.Admin);
        var category = await CreateAsync<ResourceCategoryResponse>(officer, "/api/resources/categories", new ResourceCategoryRequest("Seeds", null));
        var resource = await CreateAsync<ResourceResponse>(officer, "/api/resources", new ResourceRequest(category.Id, null, "Paddy Seed", "kg", true));

        var updated = await officer.PutAsJsonAsync($"/api/resources/{resource.Id}", new ResourceRequest(category.Id, null, "Paddy Seed B", "kg", true));
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        Assert.Equal("Paddy Seed B", (await updated.Content.ReadFromJsonAsync<ResourceResponse>(Json))!.Name);

        var invalid = await officer.PutAsJsonAsync($"/api/resources/{resource.Id}", new ResourceRequest(category.Id, null, "", "kg", true));
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await officer.PutAsJsonAsync($"/api/resources/{Guid.NewGuid()}", new ResourceRequest(category.Id, null, "X", "kg", true))).StatusCode);

        var inUse = await officer.DeleteAsync($"/api/resources/categories/{category.Id}");
        Assert.Equal(HttpStatusCode.Conflict, inUse.StatusCode);

        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync($"/api/resources/{resource.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await officer.DeleteAsync($"/api/resources/{resource.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await officer.DeleteAsync($"/api/resources/categories/{category.Id}")).StatusCode);
    }

    private static async Task<T> CreateAsync<T>(HttpClient client, string url, object body)
    {
        var response = await client.PostAsJsonAsync(url, body);
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<T>(Json))!;
    }

    private static async Task<HttpClient> ClientForAsync(WebApplicationFactory<Program> factory, ApplicationRole role)
    {
        var email = $"{role.ToString().ToLowerInvariant()}@resources.test";
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            dbContext.Users.Add(new AppUser
            {
                FullName = $"Test {role}",
                Email = email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("Resource@2026"),
                Role = role,
                IsActive = true,
                MustChangePassword = false,
                PasswordChangedAt = DateTime.UtcNow,
                TokenVersion = 1
            });
            await dbContext.SaveChangesAsync();
        }

        var client = factory.CreateClient();
        var login = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, "Resource@2026"));
        Assert.True(login.IsSuccessStatusCode, await login.Content.ReadAsStringAsync());
        var session = await login.Content.ReadFromJsonAsync<AuthResponse>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session!.AccessToken);
        return client;
    }

    private static WebApplicationFactory<Program> CreateFactory() =>
        new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Testing");
                builder.ConfigureAppConfiguration((_, config) =>
                {
                    config.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["Jwt:Secret"] = "test-jwt-secret-with-at-least-32-chars",
                        ["Jwt:Issuer"] = "AgriAssist",
                        ["Jwt:Audience"] = "AgriAssistUsers",
                        ["Jwt:ExpiryMinutes"] = "60",
                        ["Jwt:PasswordChangeExpiryMinutes"] = "10"
                    });
                });
            });
}
