using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using AgriAssist.Api.Data;
using AgriAssist.Api.Dtos.CropPlanning;
using AgriAssist.Api.Dtos.Shared;
using AgriAssist.Api.Models.CropPlanning;
using AgriAssist.Api.Models.Shared;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AgriAssist.Api.Tests;

public sealed class FarmerOnboardingIntegrationTests
{
    [Fact]
    public async Task Farmer_without_owned_farm_is_sent_to_farm_onboarding()
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var session = await RegisterFarmerAsync(client, "empty.farmer@example.test");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);

        var response = await client.GetAsync("/api/farmer/onboarding-status");
        var status = await response.Content.ReadFromJsonAsync<FarmerOnboardingStatusResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("farm", status?.Stage);
        Assert.Equal(0, status?.ActiveFarmCount);
        Assert.Equal(0, status?.ActiveFieldCount);
    }

    [Fact]
    public async Task Farmer_with_owned_farm_but_no_active_field_is_sent_to_field_onboarding()
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var session = await RegisterFarmerAsync(client, "field.onboarding@example.test");

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var farmer = await dbContext.Users.SingleAsync(user => user.Email == "field.onboarding@example.test");
            var otherFarmer = new AppUser
            {
                FullName = "Other Farmer",
                Email = "other.onboarding@example.test",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("another secure passphrase"),
                Role = ApplicationRole.Farmer,
                IsActive = true,
                PasswordChangedAt = DateTime.UtcNow,
                TokenVersion = 1
            };
            var ownedFarm = new Farm { Name = "Owned Farm", Location = "North", TotalArea = 10, OwnerUser = farmer };
            var deletedFarm = new Farm { Name = "Deleted Farm", Location = "North", TotalArea = 10, OwnerUser = farmer, IsDeleted = true };
            var otherFarm = new Farm { Name = "Other Farm", Location = "South", TotalArea = 10, OwnerUser = otherFarmer };
            dbContext.AddRange(
                otherFarmer,
                ownedFarm,
                deletedFarm,
                otherFarm,
                new Field { Name = "Inactive", SoilType = "Loam", Area = 2, Farm = ownedFarm, IsActive = false },
                new Field { Name = "Deleted", SoilType = "Loam", Area = 2, Farm = ownedFarm, IsActive = true, IsDeleted = true },
                new Field { Name = "On deleted farm", SoilType = "Loam", Area = 2, Farm = deletedFarm, IsActive = true },
                new Field { Name = "Other field", SoilType = "Loam", Area = 2, Farm = otherFarm, IsActive = true });
            await dbContext.SaveChangesAsync();
        }

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);
        var status = await client.GetFromJsonAsync<FarmerOnboardingStatusResponse>("/api/farmer/onboarding-status");

        Assert.Equal("field", status?.Stage);
        Assert.Equal(1, status?.ActiveFarmCount);
        Assert.Equal(0, status?.ActiveFieldCount);
    }

    [Fact]
    public async Task Farmer_with_active_field_on_owned_farm_is_sent_to_dashboard()
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var session = await RegisterFarmerAsync(client, "complete.onboarding@example.test");

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var farmer = await dbContext.Users.SingleAsync(user => user.Email == "complete.onboarding@example.test");
            var farm = new Farm { Name = "Ready Farm", Location = "East", TotalArea = 10, OwnerUser = farmer };
            dbContext.AddRange(farm, new Field { Name = "Ready Field", SoilType = "Clay", Area = 4, Farm = farm, IsActive = true });
            await dbContext.SaveChangesAsync();
        }

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);
        var status = await client.GetFromJsonAsync<FarmerOnboardingStatusResponse>("/api/farmer/onboarding-status");

        Assert.Equal("complete", status?.Stage);
        Assert.Equal(1, status?.ActiveFarmCount);
        Assert.Equal(1, status?.ActiveFieldCount);
    }

    [Fact]
    public async Task Non_farmer_cannot_read_farmer_onboarding_status()
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        await TestUserSeeder.AddAdminAsync(factory);
        var login = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest("admin@agriassist.local", "Admin@2026"));
        var session = await login.Content.ReadFromJsonAsync<AuthResponse>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session!.AccessToken);

        var response = await client.GetAsync("/api/farmer/onboarding-status");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private static async Task<AuthResponse> RegisterFarmerAsync(HttpClient client, string email)
    {
        var response = await client.PostAsJsonAsync(
            "/api/auth/register-farmer",
            new RegisterFarmerRequest("Onboarding Farmer", email, "harvest fields safely"));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AuthResponse>())!;
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
