using AgriAssist.Api.Models.CropPlanning;
using AgriAssist.Api.Models.Shared;
using Microsoft.EntityFrameworkCore;

namespace AgriAssist.Api.Data;

public static class SeedData
{
    public static async Task SeedAsync(AppDbContext dbContext)
    {
        if (!await dbContext.Users.AnyAsync())
        {
            dbContext.Users.AddRange(
                new AppUser
                {
                    FullName = "Development Admin",
                    Email = "admin@agriassist.local",
                    Role = ApplicationRole.Admin,
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword("Admin@2026"),
                    IsActive = true
                },
                new AppUser
                {
                    FullName = "Demo Resource Officer",
                    Email = "resourceofficer@agriassist.local",
                    Role = ApplicationRole.ResourceOfficer,
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword("Resource@2026"),
                    IsActive = true
                },
                new AppUser
                {
                    FullName = "Demo Field Officer",
                    Email = "fieldofficer@agriassist.local",
                    Role = ApplicationRole.FieldOfficer,
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword("Field@2026"),
                    IsActive = true
                },
                new AppUser
                {
                    FullName = "Demo Agricultural Officer",
                    Email = "agofficer@agriassist.local",
                    Role = ApplicationRole.AgriculturalOfficer,
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword("Agri@2026"),
                    IsActive = true
                },
                new AppUser
                {
                    FullName = "Demo Farmer",
                    Email = "farmer@agriassist.local",
                    Role = ApplicationRole.Farmer,
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword("Farmer@2026"),
                    IsActive = true
                });
        }

        if (!await dbContext.CropTypes.AnyAsync())
        {
            dbContext.CropTypes.AddRange(
                new CropType { Name = "Rice", Description = "Demo crop name for development.", IsActive = true },
                new CropType { Name = "Maize", Description = "Demo crop name for development.", IsActive = true },
                new CropType { Name = "Vegetables", Description = "Demo crop category for development.", IsActive = true });
        }

        await dbContext.SaveChangesAsync();
    }
}


