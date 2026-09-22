using AgriAssist.Api.Data;
using AgriAssist.Api.Models.Shared;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace AgriAssist.Api.Tests;

internal static class TestUserSeeder
{
    public static async Task<AppUser> AddAdminAsync(WebApplicationFactory<Program> factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var admin = new AppUser
        {
            FullName = "Test Admin",
            Email = "admin@agriassist.local",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Admin@2026"),
            Role = ApplicationRole.Admin,
            IsActive = true,
            MustChangePassword = false,
            PasswordChangedAt = DateTime.UtcNow,
            TokenVersion = 1
        };
        dbContext.Users.Add(admin);
        await dbContext.SaveChangesAsync();
        return admin;
    }
}
