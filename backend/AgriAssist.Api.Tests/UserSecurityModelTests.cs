using AgriAssist.Api.Models.Shared;

namespace AgriAssist.Api.Tests;

public sealed class UserSecurityModelTests
{
    [Fact]
    public void New_user_has_approved_security_defaults()
    {
        var user = new AppUser();

        Assert.Equal(1, user.TokenVersion);
        Assert.False(user.MustChangePassword);
        Assert.Null(user.PasswordChangedAt);
    }
}
