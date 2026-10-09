using Microsoft.EntityFrameworkCore;

namespace AgriAssist.Api.Data;

public static class AppDbContextRegistration
{
    public static void AddAppDbContext(
        IServiceCollection services,
        IConfiguration configuration,
        string environmentName)
    {
        if (string.Equals(environmentName, "Testing", StringComparison.OrdinalIgnoreCase))
        {
            var databaseName = $"AgriAssistTesting-{Guid.NewGuid():N}";
            services.AddDbContext<AppDbContext>(options =>
                options.UseInMemoryDatabase(databaseName));
            return;
        }

        var connectionString = configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            if (!string.Equals(environmentName, "Development", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "ConnectionStrings:DefaultConnection must be configured outside the Development and Testing environments.");
            }

            services.AddDbContext<AppDbContext>(options =>
                options.UseInMemoryDatabase("AgriAssistDevelopment"));
            return;
        }

        var npgsqlConnectionString = RenderDatabaseConnectionString.Normalize(connectionString);
        services.AddDbContext<AppDbContext>(options => options.UseNpgsql(npgsqlConnectionString));
    }
}
