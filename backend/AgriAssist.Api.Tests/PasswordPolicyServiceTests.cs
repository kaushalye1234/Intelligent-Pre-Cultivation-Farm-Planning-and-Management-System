using AgriAssist.Api.Configuration;
using AgriAssist.Api.Services.Shared;
using Microsoft.Extensions.Options;

namespace AgriAssist.Api.Tests;

public sealed class PasswordPolicyServiceTests
{
    [Fact]
    public async Task Accepts_supported_long_passphrase()
    {
        var service = CreateService();

        var violation = await service.ValidateAsync(
            "correct horse battery staple",
            "farmer@example.com",
            "Example Farmer",
            CancellationToken.None);

        Assert.Null(violation);
    }

    [Fact]
    public async Task Rejects_password_shorter_than_twelve_characters()
    {
        var service = CreateService();

        var violation = await service.ValidateAsync(
            "short-pass",
            "farmer@example.com",
            "Example Farmer",
            CancellationToken.None);

        Assert.Equal("PASSWORD_TOO_WEAK", violation?.Code);
    }

    [Fact]
    public async Task Rejects_password_over_bcrypt_utf8_limit_without_truncation()
    {
        var service = CreateService();

        var violation = await service.ValidateAsync(
            string.Concat(Enumerable.Repeat("🌾", 19)),
            "farmer@example.com",
            "Example Farmer",
            CancellationToken.None);

        Assert.Equal("PASSWORD_EXCEEDS_HASH_LIMIT", violation?.Code);
    }

    [Theory]
    [InlineData("FARMER@EXAMPLE.COM")]
    [InlineData("ExampleFarmer")]
    public async Task Rejects_obvious_account_identifiers(string password)
    {
        var service = CreateService();

        var violation = await service.ValidateAsync(
            password,
            "farmer@example.com",
            "Example Farmer",
            CancellationToken.None);

        Assert.Equal("PASSWORD_IDENTIFIER_MATCH", violation?.Code);
    }

    [Fact]
    public async Task Rejects_configured_compromised_password()
    {
        var service = CreateService("known compromised phrase");

        var violation = await service.ValidateAsync(
            "Known Compromised Phrase",
            "farmer@example.com",
            "Example Farmer",
            CancellationToken.None);

        Assert.Equal("PASSWORD_COMPROMISED", violation?.Code);
    }

    private static PasswordPolicyService CreateService(params string[] compromisedPasswords)
    {
        var options = Options.Create(new PasswordSecurityOptions
        {
            CompromisedPasswords = compromisedPasswords
        });

        return new PasswordPolicyService(new ConfiguredCompromisedPasswordChecker(options));
    }
}
