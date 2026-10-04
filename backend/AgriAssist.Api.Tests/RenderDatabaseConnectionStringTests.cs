using AgriAssist.Api.Data;
using Npgsql;

namespace AgriAssist.Api.Tests;

public sealed class RenderDatabaseConnectionStringTests
{
    [Fact]
    public void Normalize_ConvertsRenderPostgresUriToNpgsqlConnectionString()
    {
        var actual = RenderDatabaseConnectionString.Normalize(
            "postgresql://farm-user:p%40ss@db.internal:5432/farm_db");

        var parsed = new NpgsqlConnectionStringBuilder(actual);
        Assert.Equal("db.internal", parsed.Host);
        Assert.Equal(5432, parsed.Port);
        Assert.Equal("farm_db", parsed.Database);
        Assert.Equal("farm-user", parsed.Username);
        Assert.Equal("p@ss", parsed.Password);
        Assert.Equal(SslMode.Require, parsed.SslMode);
    }

    [Fact]
    public void Normalize_LeavesRegularNpgsqlConnectionStringUnchanged()
    {
        const string connectionString = "Host=localhost;Database=farm;Username=postgres;Password=test";

        Assert.Equal(connectionString, RenderDatabaseConnectionString.Normalize(connectionString));
    }

    [Theory]
    [InlineData("postgresql://missing-host/farm")]
    [InlineData("https://example.test/farm")]
    public void Normalize_RejectsUnsupportedOrIncompleteUris(string connectionString)
    {
        Assert.Throws<InvalidOperationException>(() =>
            RenderDatabaseConnectionString.Normalize(connectionString));
    }
}
