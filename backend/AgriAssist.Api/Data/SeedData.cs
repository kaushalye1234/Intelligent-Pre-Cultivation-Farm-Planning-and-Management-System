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

        // Keep the local development workflow usable end to end. The AI coordinator
        // requires at least one verified profile before it can create downstream
        // field and weather/resource steps.
        var referenceCropTypes = await dbContext.CropTypes
            .Where(item => item.IsActive && !item.IsDeleted)
            .ToListAsync();

        foreach (var cropType in referenceCropTypes)
        {
            if (await dbContext.CropReferenceProfiles.AnyAsync(item => item.CropTypeId == cropType.Id && item.IsActive && !item.IsDeleted))
            {
                continue;
            }

            var verifiedAt = DateTime.UtcNow;
            dbContext.CropReferenceProfiles.Add(new CropReferenceProfile
            {
                CropTypeId = cropType.Id,
                VarietyName = $"Demo {cropType.Name.ToLowerInvariant()}",
                Region = "Sri Lanka",
                SourceName = "AgriAssist development reference",
                SourceVersion = "dev-1",
                VerifiedAt = verifiedAt,
                IsActive = true,
                Stages =
                [
                    new CropStageReference { StageName = "Establishment", Sequence = 1, TypicalMinDays = 0, TypicalMaxDays = 30, SourceName = "AgriAssist development reference" },
                    new CropStageReference { StageName = "Vegetative", Sequence = 2, TypicalMinDays = 31, TypicalMaxDays = 75, SourceName = "AgriAssist development reference" },
                    new CropStageReference { StageName = "Maturity", Sequence = 3, TypicalMinDays = 76, TypicalMaxDays = 120, SourceName = "AgriAssist development reference" }
                ],
                Rules =
                [
                    new CropRuleReference { RuleType = "Water", RuleKey = "irrigation", StructuredValueJson = "{\"requirement\":\"regular\"}", SourceName = "AgriAssist development reference", VerifiedAt = verifiedAt },
                    new CropRuleReference { RuleType = "Soil", RuleKey = "preferred", StructuredValueJson = "{\"types\":[\"Loam\",\"Clay loam\"]}", SourceName = "AgriAssist development reference", VerifiedAt = verifiedAt }
                ]
            });
        }

        await dbContext.SaveChangesAsync();
    }
}


