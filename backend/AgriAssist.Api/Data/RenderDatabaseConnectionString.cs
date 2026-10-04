using Npgsql;

namespace AgriAssist.Api.Data;

public static class RenderDatabaseConnectionString
{
    public static string Normalize(string connectionString)
    {
        if (!Uri.TryCreate(connectionString, UriKind.Absolute, out var uri)
            || (uri.Scheme != "postgres" && uri.Scheme != "postgresql"))
        {
            if (connectionString.Contains("://", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("The database URL must use the PostgreSQL URI scheme.");
            }

            return connectionString;
        }

        var database = Uri.UnescapeDataString(uri.AbsolutePath.Trim('/'));
        if (string.IsNullOrWhiteSpace(uri.Host)
            || string.IsNullOrWhiteSpace(uri.UserInfo)
            || string.IsNullOrWhiteSpace(database))
        {
            throw new InvalidOperationException("The PostgreSQL database URL must include host, credentials, and database name.");
        }

        var credentials = uri.UserInfo.Split(':', 2);
        if (credentials.Length != 2)
        {
            throw new InvalidOperationException("The PostgreSQL database URL must include a username and password.");
        }

        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = uri.Host,
            Port = uri.IsDefaultPort ? 5432 : uri.Port,
            Database = database,
            Username = Uri.UnescapeDataString(credentials[0]),
            Password = Uri.UnescapeDataString(credentials[1]),
            SslMode = SslMode.Require
        };

        var query = uri.Query.TrimStart('?');
        foreach (var part in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var item = part.Split('=', 2);
            if (item.Length == 2 && string.Equals(Uri.UnescapeDataString(item[0]), "sslmode", StringComparison.OrdinalIgnoreCase))
            {
                builder.SslMode = Uri.UnescapeDataString(item[1]).ToLowerInvariant() switch
                {
                    "disable" => SslMode.Disable,
                    "allow" => SslMode.Allow,
                    "prefer" => SslMode.Prefer,
                    "require" => SslMode.Require,
                    "verify-ca" => SslMode.VerifyCA,
                    "verify-full" => SslMode.VerifyFull,
                    var value => throw new InvalidOperationException($"Unsupported PostgreSQL sslmode '{value}'.")
                };
            }
        }

        return builder.ConnectionString;
    }
}
