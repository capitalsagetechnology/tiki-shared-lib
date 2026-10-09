using Tiki.Shared.Auth.Authorization;
using Xunit;

namespace Tiki.Shared.Tests.Auth.Authorization;

public class CommissionsModuleTests
{
    [Fact]
    public void Commissions_formats_as_the_permission_strings_services_enforce()
    {
        Assert.Equal("commissions:read", TikiPermission.Format(TikiModule.Commissions, PermissionAction.Read));
        Assert.Equal("commissions:write", TikiPermission.Format(TikiModule.Commissions, PermissionAction.Write));
    }

    /// <summary>Appended, never inserted: an earlier module keeping its value is what makes the change additive.</summary>
    [Fact]
    public void Commissions_comes_after_every_existing_module()
    {
        Assert.Equal(TikiModule.Settings + 1, TikiModule.Commissions);
    }
}
