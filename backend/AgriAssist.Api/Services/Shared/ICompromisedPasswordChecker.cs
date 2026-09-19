namespace AgriAssist.Api.Services.Shared;

public interface ICompromisedPasswordChecker
{
    ValueTask<bool> IsCompromisedAsync(string password, CancellationToken cancellationToken);
}
