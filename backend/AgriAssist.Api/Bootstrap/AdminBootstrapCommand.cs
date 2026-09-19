using AgriAssist.Api.Services.Shared;

namespace AgriAssist.Api.Bootstrap;

public sealed class AdminBootstrapCommand(IAdminBootstrapService bootstrapService)
{
    public async Task<int> ExecuteAsync(CancellationToken cancellationToken)
    {
        if (await bootstrapService.AnyAdminExistsAsync(cancellationToken))
        {
            Console.Error.WriteLine("ADMIN_ALREADY_EXISTS: Admin bootstrap is disabled.");
            return 2;
        }

        if (Console.IsInputRedirected)
        {
            Console.Error.WriteLine("Bootstrap requires an interactive console so the password is not exposed.");
            return 3;
        }

        Console.Write("Full name: ");
        var fullName = Console.ReadLine() ?? string.Empty;
        Console.Write("Email: ");
        var email = Console.ReadLine() ?? string.Empty;
        Console.Write("Password: ");
        var password = ReadPassword();
        Console.WriteLine();

        try
        {
            var admin = await bootstrapService.BootstrapAsync(
                fullName,
                email,
                password,
                cancellationToken);
            Console.WriteLine($"Admin account created for {admin.Email}.");
            return 0;
        }
        catch (ApiException exception)
        {
            Console.Error.WriteLine($"{exception.Code}: {exception.Message}");
            return 1;
        }
        finally
        {
            password = string.Empty;
        }
    }

    private static string ReadPassword()
    {
        var characters = new List<char>();
        while (true)
        {
            var key = Console.ReadKey(intercept: true);
            if (key.Key == ConsoleKey.Enter)
            {
                return new string(characters.ToArray());
            }

            if (key.Key == ConsoleKey.Backspace)
            {
                if (characters.Count > 0)
                {
                    characters.RemoveAt(characters.Count - 1);
                }

                continue;
            }

            if (!char.IsControl(key.KeyChar))
            {
                characters.Add(key.KeyChar);
            }
        }
    }
}
