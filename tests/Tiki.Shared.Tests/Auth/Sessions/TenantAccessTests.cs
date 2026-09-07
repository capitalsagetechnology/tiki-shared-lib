using Tiki.Shared.Auth.Authorization;
using Tiki.Shared.Auth.Sessions;
using Xunit;

namespace Tiki.Shared.Tests.Auth.Sessions;

/// <summary>
/// The multi-tenant access rules. A user holding Admin in one tenant and read-only in another
/// is the normal case now, so "what may this person do" is only answerable together with
/// "in which tenant" — these pin that the pair is never collapsed.
/// </summary>
public class TenantAccessTests
{
    private static readonly Guid TenantA = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
    private static readonly Guid TenantB = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000002");
    private static readonly Guid TenantC = Guid.Parse("cccccccc-0000-0000-0000-000000000003");

    private static string P(TikiModule module, PermissionAction action) => TikiPermission.Format(module, action);

    private static TikiSession Session(
        IReadOnlyList<string>? global = null,
        Dictionary<Guid, TenantGrant>? tenants = null,
        Guid? home = null) => new()
    {
        SessionId = Guid.NewGuid(),
        UserId = Guid.NewGuid(),
        UserType = "TeamMember",
        GlobalPermissions = global ?? [],
        TenantAccess = tenants ?? [],
        HomeTenantId = home,
        IssuedAt = DateTimeOffset.UtcNow,
        ExpiresAt = DateTimeOffset.UtcNow.AddDays(1),
    };

    private static TenantGrant Grant(Guid tenantId, params string[] permissions) => new()
    {
        TenantId = tenantId,
        Roles = ["TestRole"],
        Permissions = permissions,
    };

    [Fact]
    public void Permissions_in_one_tenant_do_not_leak_into_another()
    {
        // The single most important property of the model: an Admin in Tenant A must not be an
        // Admin in Tenant B. A flat permission list on the session would make this impossible
        // to express, which is why the session carries a map.
        var session = Session(tenants: new()
        {
            [TenantA] = Grant(TenantA, P(TikiModule.Users, PermissionAction.Write)),
            [TenantB] = Grant(TenantB, P(TikiModule.Users, PermissionAction.Read)),
        });

        Assert.True(session.HasPermission(TikiModule.Users, PermissionAction.Write, TenantA));
        Assert.False(session.HasPermission(TikiModule.Users, PermissionAction.Write, TenantB));
    }

    [Fact]
    public void A_global_grant_applies_in_every_tenant_including_unknown_ones()
    {
        // What "Global Admin" means mechanically. TenantC is not in the access map at all —
        // it stands in for a tenant created after this session began.
        var session = Session(global: [P(TikiModule.Tenants, PermissionAction.Write)]);

        Assert.True(session.HasPermission(TikiModule.Tenants, PermissionAction.Write, TenantA));
        Assert.True(session.HasPermission(TikiModule.Tenants, PermissionAction.Write, TenantC));
        Assert.True(session.CanAccessTenant(TenantC));
    }

    [Fact]
    public void Global_and_tenant_grants_combine_rather_than_replace_each_other()
    {
        var session = Session(
            global: [P(TikiModule.Reports, PermissionAction.Read)],
            tenants: new() { [TenantA] = Grant(TenantA, P(TikiModule.Users, PermissionAction.Write)) });

        Assert.True(session.HasPermission(TikiModule.Reports, PermissionAction.Read, TenantA));
        Assert.True(session.HasPermission(TikiModule.Users, PermissionAction.Write, TenantA));
    }

    [Fact]
    public void A_tenant_scoped_session_cannot_reach_a_tenant_it_was_not_granted()
    {
        var session = Session(tenants: new() { [TenantA] = Grant(TenantA, P(TikiModule.Users, PermissionAction.Read)) });

        Assert.True(session.CanAccessTenant(TenantA));
        Assert.False(session.CanAccessTenant(TenantB));
    }

    [Fact]
    public void Write_implies_read_once_expanded()
    {
        // Expansion happens at grant time, so a stored session is already the complete answer
        // and no permission check has to know the implication rule.
        var expanded = TikiPermission.Expand([new TikiPermission(TikiModule.Wallets, PermissionAction.Write)]);

        Assert.Contains(P(TikiModule.Wallets, PermissionAction.Write), expanded);
        Assert.Contains(P(TikiModule.Wallets, PermissionAction.Read), expanded);
    }

    [Fact]
    public void Read_does_not_imply_write()
    {
        var expanded = TikiPermission.Expand([new TikiPermission(TikiModule.Wallets, PermissionAction.Read)]);

        Assert.DoesNotContain(P(TikiModule.Wallets, PermissionAction.Write), expanded);
    }

    [Fact]
    public void A_permission_round_trips_through_its_wire_form()
    {
        var original = new TikiPermission(TikiModule.Compliance, PermissionAction.Write);

        Assert.Equal("compliance:write", original.Value);
        Assert.True(TikiPermission.TryParse("compliance:write", out var parsed));
        Assert.Equal(original, parsed);
    }

    [Theory]
    [InlineData("")]
    [InlineData("wallets")]
    [InlineData("wallets:")]
    [InlineData(":read")]
    [InlineData("notamodule:read")]
    [InlineData("wallets:destroy")]
    public void An_unrecognised_permission_string_does_not_parse(string value)
    {
        // A role row carrying a module that never existed — a typo written straight into the
        // database — must fail loudly here rather than silently granting or denying.
        Assert.False(TikiPermission.TryParse(value, out _));
    }

    // --- tenant selection -----------------------------------------------------------------

    [Fact]
    public void An_explicit_selection_the_session_grants_is_used()
    {
        var session = Session(tenants: new()
        {
            [TenantA] = Grant(TenantA), [TenantB] = Grant(TenantB),
        });

        var result = TenantSelection.Resolve(session, TenantB);

        Assert.False(result.IsDenied);
        Assert.Equal(TenantB, result.TenantId);
    }

    [Fact]
    public void An_explicit_selection_the_session_does_not_grant_is_denied_not_substituted()
    {
        // Substituting a tenant the user does have would show them one tenant's data under a
        // heading naming another. Denial is the only safe answer.
        var session = Session(tenants: new() { [TenantA] = Grant(TenantA) });

        var result = TenantSelection.Resolve(session, TenantB);

        Assert.True(result.IsDenied);
        Assert.Null(result.TenantId);
    }

    [Fact]
    public void With_no_selection_a_single_accessible_tenant_is_used()
    {
        var session = Session(tenants: new() { [TenantA] = Grant(TenantA) });

        Assert.Equal(TenantA, TenantSelection.Resolve(session, null).TenantId);
    }

    [Fact]
    public void With_no_selection_and_several_tenants_nothing_is_chosen()
    {
        // The caller is made to choose. Picking the first is how someone edits the wrong
        // tenant's settings without noticing.
        var session = Session(tenants: new()
        {
            [TenantA] = Grant(TenantA), [TenantB] = Grant(TenantB),
        });

        var result = TenantSelection.Resolve(session, null);

        Assert.False(result.IsDenied);
        Assert.Null(result.TenantId);
    }

    [Fact]
    public void A_home_tenant_is_the_default_for_an_end_user()
    {
        var session = Session(
            tenants: new() { [TenantA] = Grant(TenantA), [TenantB] = Grant(TenantB) },
            home: TenantB);

        Assert.Equal(TenantB, TenantSelection.Resolve(session, null).TenantId);
    }

    [Fact]
    public void A_global_session_may_select_a_tenant_it_holds_no_explicit_grant_for()
    {
        var session = Session(global: [P(TikiModule.Tenants, PermissionAction.Write)]);

        var result = TenantSelection.Resolve(session, TenantC);

        Assert.False(result.IsDenied);
        Assert.Equal(TenantC, result.TenantId);
    }
}
