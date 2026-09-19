using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using AgriAssist.Api.Data;
using AgriAssist.Api.Dtos.Shared;
using AgriAssist.Api.Models.Shared;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AgriAssist.Api.Tests;

public sealed class AdminUsersIntegrationTests
{
    [Fact]
    public async Task Staff_creation_uses_admin_route_and_rejects_farmer_and_duplicate_email()
    {
        await using var factory = CreateFactory();
        using var adminClient = await CreateAdminClientAsync(factory);
        var request = new CreateStaffUserRequest(
            "Field Officer",
            "field.officer@example.com",
            ApplicationRole.FieldOfficer,
            "temporary field phrase",
            null);

        var created = await adminClient.PostAsJsonAsync("/api/admin/users", request);
        var createdBody = await created.Content.ReadAsStringAsync();
        var user = JsonSerializer.Deserialize<AdminUserResponse>(
            createdBody,
            new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.NotNull(user);
        Assert.True(user!.MustChangePassword);
        Assert.DoesNotContain(request.TemporaryPassword, createdBody);

        var duplicate = await adminClient.PostAsJsonAsync("/api/admin/users", request);
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        Assert.Equal("EMAIL_ALREADY_EXISTS", await ErrorCodeAsync(duplicate));

        var farmer = await adminClient.PostAsJsonAsync(
            "/api/admin/users",
            request with
            {
                Email = "not.a.staff.farmer@example.com",
                Role = ApplicationRole.Farmer
            });
        Assert.Equal(HttpStatusCode.BadRequest, farmer.StatusCode);

        var legacyCreate = await adminClient.PostAsJsonAsync(
            "/api/users",
            request with { Email = "legacy@example.com" });
        Assert.NotEqual(HttpStatusCode.Created, legacyCreate.StatusCode);
    }

    [Fact]
    public async Task Additional_admin_requires_reauthentication_without_invalidating_actor_session()
    {
        await using var factory = CreateFactory();
        using var adminClient = await CreateAdminClientAsync(factory);
        var request = new CreateStaffUserRequest(
            "Second Admin",
            "second.admin@example.com",
            ApplicationRole.Admin,
            "temporary admin phrase",
            "wrong-current-password");

        var rejected = await adminClient.PostAsJsonAsync("/api/admin/users", request);
        Assert.Equal(HttpStatusCode.Forbidden, rejected.StatusCode);
        Assert.Equal("ADMIN_REAUTHENTICATION_FAILED", await ErrorCodeAsync(rejected));

        var profileAfterFailure = await adminClient.GetAsync("/api/auth/profile");
        Assert.Equal(HttpStatusCode.OK, profileAfterFailure.StatusCode);

        var created = await adminClient.PostAsJsonAsync(
            "/api/admin/users",
            request with { CurrentAdminPassword = "Admin@2026" });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
    }

    [Fact]
    public async Task Staff_reset_invalidates_old_token_and_issues_only_a_temporary_login_state()
    {
        await using var factory = CreateFactory();
        using var adminClient = await CreateAdminClientAsync(factory);
        var created = await adminClient.PostAsJsonAsync(
            "/api/admin/users",
            new CreateStaffUserRequest(
                "Resource Officer",
                "resource.officer@example.com",
                ApplicationRole.ResourceOfficer,
                "initial temporary phrase",
                null));
        var staff = await created.Content.ReadFromJsonAsync<AdminUserResponse>();

        using var staffClient = factory.CreateClient();
        var temporaryLogin = await staffClient.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest("resource.officer@example.com", "initial temporary phrase"));
        var temporarySession = await temporaryLogin.Content.ReadFromJsonAsync<AuthResponse>();
        staffClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", temporarySession!.PasswordChangeToken);
        var changed = await staffClient.PostAsJsonAsync(
            "/api/auth/change-temporary-password",
            new ChangeTemporaryPasswordRequest("permanent resource phrase"));
        var normalSession = await changed.Content.ReadFromJsonAsync<AuthResponse>();

        var resetRequest = new ResetStaffPasswordRequest("replacement temporary phrase", null);
        var reset = await adminClient.PostAsJsonAsync(
            $"/api/admin/users/{staff!.Id}/reset-password",
            resetRequest);

        Assert.Equal(HttpStatusCode.NoContent, reset.StatusCode);
        Assert.Empty(await reset.Content.ReadAsStringAsync());

        staffClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", normalSession!.AccessToken);
        var oldSessionProfile = await staffClient.GetAsync("/api/auth/profile");
        Assert.Equal(HttpStatusCode.Unauthorized, oldSessionProfile.StatusCode);

        staffClient.DefaultRequestHeaders.Authorization = null;
        var replacementLogin = await staffClient.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest("resource.officer@example.com", resetRequest.TemporaryPassword));
        var replacementSession = await replacementLogin.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.Equal("passwordChangeRequired", replacementSession?.AuthenticationStatus);
        Assert.Null(replacementSession?.AccessToken);

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var storedUser = await dbContext.Users.SingleAsync(item => item.Id == staff.Id);
        Assert.True(storedUser.MustChangePassword);
        Assert.Null(storedUser.PasswordChangedAt);
        Assert.Equal(3, storedUser.TokenVersion);
    }

    [Fact]
    public async Task Final_active_admin_cannot_be_deactivated_or_demoted()
    {
        await using var factory = CreateFactory();
        var admin = await TestUserSeeder.AddAdminAsync(factory);
        using var adminClient = factory.CreateClient();
        var login = await adminClient.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest("admin@agriassist.local", "Admin@2026"));
        var session = await login.Content.ReadFromJsonAsync<AuthResponse>();
        adminClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", session!.AccessToken);

        var deactivate = await adminClient.PatchAsJsonAsync(
            $"/api/users/{admin.Id}/active",
            new SetUserActiveRequest(false));
        Assert.Equal(HttpStatusCode.Conflict, deactivate.StatusCode);
        Assert.Equal("LAST_ACTIVE_ADMIN_REQUIRED", await ErrorCodeAsync(deactivate));

        var demote = await adminClient.PatchAsJsonAsync(
            $"/api/users/{admin.Id}/role",
            new UpdateUserRoleRequest(ApplicationRole.FieldOfficer));
        Assert.Equal(HttpStatusCode.Conflict, demote.StatusCode);
        Assert.Equal("LAST_ACTIVE_ADMIN_REQUIRED", await ErrorCodeAsync(demote));
    }

    [Fact]
    public async Task Role_and_active_state_transitions_increment_token_version()
    {
        await using var factory = CreateFactory();
        using var adminClient = await CreateAdminClientAsync(factory);
        var created = await adminClient.PostAsJsonAsync(
            "/api/admin/users",
            new CreateStaffUserRequest(
                "Field Officer",
                "transition.officer@example.com",
                ApplicationRole.FieldOfficer,
                "temporary field phrase",
                null));
        var staff = await created.Content.ReadFromJsonAsync<AdminUserResponse>();

        var deactivated = await adminClient.PatchAsJsonAsync(
            $"/api/users/{staff!.Id}/active",
            new SetUserActiveRequest(false));
        var reactivated = await adminClient.PatchAsJsonAsync(
            $"/api/users/{staff.Id}/active",
            new SetUserActiveRequest(true));
        var roleChanged = await adminClient.PatchAsJsonAsync(
            $"/api/users/{staff.Id}/role",
            new UpdateUserRoleRequest(ApplicationRole.ResourceOfficer));

        Assert.Equal(HttpStatusCode.OK, deactivated.StatusCode);
        Assert.Equal(HttpStatusCode.OK, reactivated.StatusCode);
        Assert.Equal(HttpStatusCode.OK, roleChanged.StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var storedUser = await dbContext.Users.SingleAsync(item => item.Id == staff.Id);
        Assert.Equal(4, storedUser.TokenVersion);
    }

    [Fact]
    public async Task Resetting_another_admin_requires_reauthentication_and_failed_attempt_keeps_actor_session()
    {
        await using var factory = CreateFactory();
        using var adminClient = await CreateAdminClientAsync(factory);
        var created = await adminClient.PostAsJsonAsync(
            "/api/admin/users",
            new CreateStaffUserRequest(
                "Second Admin",
                "reset.admin@example.com",
                ApplicationRole.Admin,
                "temporary admin phrase",
                "Admin@2026"));
        var target = await created.Content.ReadFromJsonAsync<AdminUserResponse>();

        var rejected = await adminClient.PostAsJsonAsync(
            $"/api/admin/users/{target!.Id}/reset-password",
            new ResetStaffPasswordRequest("replacement admin phrase", "wrong-current-password"));
        Assert.Equal(HttpStatusCode.Forbidden, rejected.StatusCode);
        Assert.Equal("ADMIN_REAUTHENTICATION_FAILED", await ErrorCodeAsync(rejected));

        var profileAfterFailure = await adminClient.GetAsync("/api/auth/profile");
        Assert.Equal(HttpStatusCode.OK, profileAfterFailure.StatusCode);

        var reset = await adminClient.PostAsJsonAsync(
            $"/api/admin/users/{target.Id}/reset-password",
            new ResetStaffPasswordRequest("replacement admin phrase", "Admin@2026"));
        Assert.Equal(HttpStatusCode.NoContent, reset.StatusCode);
    }

    [Fact]
    public async Task Non_admin_cannot_use_staff_creation_endpoint()
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var registration = await client.PostAsJsonAsync(
            "/api/auth/register-farmer",
            new RegisterFarmerRequest(
                "Unauthorized Farmer",
                "unauthorized.farmer@example.com",
                "secure farmer phrase"));
        var farmerSession = await registration.Content.ReadFromJsonAsync<AuthResponse>();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", farmerSession!.AccessToken);

        var response = await client.PostAsJsonAsync(
            "/api/admin/users",
            new CreateStaffUserRequest(
                "Unauthorized Officer",
                "unauthorized.officer@example.com",
                ApplicationRole.FieldOfficer,
                "temporary field phrase",
                null));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("ROLE_NOT_AUTHORIZED", await ErrorCodeAsync(response));
    }

    private static async Task<HttpClient> CreateAdminClientAsync(WebApplicationFactory<Program> factory)
    {
        await TestUserSeeder.AddAdminAsync(factory);
        var client = factory.CreateClient();
        var login = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest("admin@agriassist.local", "Admin@2026"));
        var session = await login.Content.ReadFromJsonAsync<AuthResponse>();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", session!.AccessToken);
        return client;
    }

    private static async Task<string?> ErrorCodeAsync(HttpResponseMessage response)
    {
        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return payload.RootElement.GetProperty("error").GetProperty("code").GetString();
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
