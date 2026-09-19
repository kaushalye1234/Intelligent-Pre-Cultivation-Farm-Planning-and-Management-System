using AgriAssist.Api.Models.CropPlanning;
using Microsoft.EntityFrameworkCore;

namespace AgriAssist.Api.Data;

public static class SeedData
{
    public static async Task SeedAsync(AppDbContext dbContext)
    {
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


