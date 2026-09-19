using AgriAssist.Api.Models.Shared;

namespace AgriAssist.Api.Services.Shared;

public interface IAdminBootstrapService
{
    Task<bool> AnyAdminExistsAsync(CancellationToken cancellationToken);

    Task<AppUser> BootstrapAsync(
        string fullName,
        string email,
        string password,
        CancellationToken cancellationToken);
}
