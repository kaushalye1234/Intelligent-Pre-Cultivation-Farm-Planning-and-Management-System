using AgriAssist.Api.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AgriAssist.Api.Tests;

public sealed class ProductionDatabaseConfigurationTests
{
    [Fact]
    public void Production_without_connection_string_fails_startup()
    {
        using var factory = CreateFactory("Production", null);

        var exception = Assert.ThrowsAny<Exception>(() => factory.CreateClient());

        Assert.Contains("ConnectionStrings:DefaultConnection", exception.ToString());
    }

    [Fact]
    public void Production_with_connection_string_uses_postgresql_provider()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] =
                    "Host=localhost;Port=5432;Database=agriassist_test;Username=test;Password=test"
            })
            .Build();
        AppDbContextRegistration.AddAppDbContext(services, configuration, "Production");

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        Assert.Equal("Npgsql.EntityFrameworkCore.PostgreSQL", dbContext.Database.ProviderName);
    }

    [Fact]
    public void Development_without_connection_string_keeps_in_memory_provider()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        var configuration = new ConfigurationBuilder().Build();
        AppDbContextRegistration.AddAppDbContext(services, configuration, "Development");

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        Assert.True(dbContext.Database.IsInMemory());
    }

    [Fact]
    public async Task Production_exposes_swagger_only_when_explicitly_enabled()
    {
        using var enabledFactory = CreateFactory(
            "Production",
            "Host=localhost;Port=5432;Database=agriassist_test;Username=test;Password=test",
            enableSwagger: true);
        using var enabledClient = enabledFactory.CreateClient();

        var enabledResponse = await enabledClient.GetAsync("/swagger/v1/swagger.json");

        Assert.Equal(System.Net.HttpStatusCode.OK, enabledResponse.StatusCode);

        using var disabledFactory = CreateFactory(
            "Production",
            "Host=localhost;Port=5432;Database=agriassist_test;Username=test;Password=test",
            enableSwagger: false);
        using var disabledClient = disabledFactory.CreateClient();

        var disabledResponse = await disabledClient.GetAsync("/swagger/v1/swagger.json");

        Assert.Equal(System.Net.HttpStatusCode.NotFound, disabledResponse.StatusCode);
    }

    private static WebApplicationFactory<Program> CreateFactory(
        string environment,
        string? connectionString,
        bool enableSwagger = false) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment(environment);
            builder.ConfigureLogging(logging => logging.ClearProviders());
            builder.UseSetting("ConnectionStrings:DefaultConnection", connectionString ?? string.Empty);
            builder.UseSetting("ApiDocs:Enabled", enableSwagger.ToString());
        });
}
