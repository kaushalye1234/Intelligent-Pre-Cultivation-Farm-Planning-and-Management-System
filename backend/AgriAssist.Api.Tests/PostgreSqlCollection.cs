namespace AgriAssist.Api.Tests;

/// <summary>Serialises every test class that migrates and writes to the shared PostgreSQL test database.</summary>
[CollectionDefinition(Name)]
public sealed class PostgreSqlCollection
{
    public const string Name = "PostgreSQL integration";
}
