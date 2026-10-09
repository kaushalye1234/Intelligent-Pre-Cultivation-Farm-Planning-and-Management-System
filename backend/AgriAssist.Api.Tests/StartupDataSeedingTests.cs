using AgriAssist.Api.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AgriAssist.Api.Tests;

public sealed class StartupDataSeedingTests
{
    [Fact]
    public async Task Startup_WithEmptyDatabase_DoesNotSeedCropCatalogData()
    {
        using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder => builder.UseEnvironment("Testing"));
        using var client = factory.CreateClient();
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        Assert.False(await dbContext.CropTypes.AnyAsync());
        Assert.False(await dbContext.CropVarieties.AnyAsync());
        Assert.False(await dbContext.CropReferenceProfiles.AnyAsync());
    }
}
