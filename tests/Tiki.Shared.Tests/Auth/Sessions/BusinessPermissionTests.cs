using Tiki.Shared.Auth.Authorization;
using Tiki.Shared.Auth.Sessions;
using Xunit;

namespace Tiki.Shared.Tests.Auth.Sessions;

/// <summary>
/// A business's own permission grid — separate from <see cref="TikiPermission"/>, which is
/// Tiki's internal admin vocabulary. These pin the two can never be confused for one another
/// even though both end up in the same session-level permission list.
/// </summary>
public class BusinessPermissionTests
{
    private static readonly Guid TenantA = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
    private static readonly Guid BusinessA = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000001");

    private static TikiSession Session(IReadOnlyList<string> homeTenantPermissions, Guid? businessId) => new()
    {
        SessionId = Guid.NewGuid(),
        UserId = Guid.NewGuid(),
        UserType = "TeamMember",
        HomeTenantId = TenantA,
        BusinessId = businessId,
        TenantAccess = new Dictionary<Guid, TenantGrant>
        {
            [TenantA] = new() { TenantId = TenantA, Roles = ["Owner"], Permissions = homeTenantPermissions },
        },
        IssuedAt = DateTimeOffset.UtcNow,
        ExpiresAt = DateTimeOffset.UtcNow.AddDays(1),
    };

    [Fact]
    public void A_permission_round_trips_through_its_wire_form()
    {
        var original = new BusinessPermission(BusinessModule.Store, PermissionAction.Write);

        Assert.Equal("business:store:write", original.Value);
        Assert.True(BusinessPermission.TryParse("business:store:write", out var parsed));
        Assert.Equal(original, parsed);
    }

    [Fact]
    public void Write_implies_read_once_expanded()
    {
        var expanded = BusinessPermission.Expand([new BusinessPermission(BusinessModule.Chargebacks, PermissionAction.Write)]);

        Assert.Contains("business:chargebacks:write", expanded);
        Assert.Contains("business:chargebacks:read", expanded);
    }

    [Fact]
    public void Read_does_not_imply_write()
    {
        var expanded = BusinessPermission.Expand([new BusinessPermission(BusinessModule.Chargebacks, PermissionAction.Read)]);

        Assert.DoesNotContain("business:chargebacks:write", expanded);
    }

    [Theory]
    [InlineData("")]
    [InlineData("store:write")] // missing the business: prefix entirely
    [InlineData("business:store")]
    [InlineData("business:store:")]
    [InlineData("business::write")]
    [InlineData("business:notamodule:write")]
    [InlineData("business:store:destroy")]
    public void An_unrecognised_permission_string_does_not_parse(string value)
    {
        Assert.False(BusinessPermission.TryParse(value, out _));
    }

    [Fact]
    public void A_tiki_permission_and_a_business_permission_never_collide_as_strings()
    {
        // The whole reason for the "business:" prefix: without it, TikiModule.Roles and
        // BusinessModule.Roles would both format to "roles:write" and one grant would silently
        // stand in for the other.
        var tiki = TikiPermission.Format(TikiModule.Roles, PermissionAction.Write);
        var business = BusinessPermission.Format(BusinessModule.Roles, PermissionAction.Write);

        Assert.NotEqual(tiki, business);
    }

    [Fact]
    public void A_session_acting_for_a_business_sees_its_folded_in_permission()
    {
        var session = Session(
            homeTenantPermissions: [BusinessPermission.Format(BusinessModule.Store, PermissionAction.Write)],
            businessId: BusinessA);

        Assert.True(session.HasBusinessPermission(BusinessModule.Store, PermissionAction.Write));
        // Expansion is materialised at grant time, same as TikiPermission — a session holding
        // only "write" in its stored list is not re-expanded to include "read" on the fly.
        Assert.False(session.HasBusinessPermission(BusinessModule.Cards, PermissionAction.Read));
    }

    [Fact]
    public void A_session_with_no_business_never_holds_a_business_permission()
    {
        // Even if the string happened to be present on the session for some other reason, a
        // session not acting for any business must not pass a business-permission check — the
        // BusinessId gate comes first.
        var session = Session(
            homeTenantPermissions: [BusinessPermission.Format(BusinessModule.Store, PermissionAction.Write)],
            businessId: null);

        Assert.False(session.HasBusinessPermission(BusinessModule.Store, PermissionAction.Write));
    }
}
