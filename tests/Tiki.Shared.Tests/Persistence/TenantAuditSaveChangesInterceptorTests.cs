using Microsoft.EntityFrameworkCore;
using Tiki.Shared.Persistence;
using Tiki.Shared.Tests.Persistence.TestSupport;
using Xunit;

namespace Tiki.Shared.Tests.Persistence;

/// <summary>Proves the interceptor actually stamps fields on a real SaveChanges call — not just that it compiles.</summary>
public class TenantAuditSaveChangesInterceptorTests
{
    private static WidgetDbContext CreateContext(out MutableTenantAccessor tenantAccessor, string? currentUser = "amaka")
    {
        var accessor = new MutableTenantAccessor();
        tenantAccessor = accessor;

        var interceptor = new TenantAuditSaveChangesInterceptor(() => accessor.CurrentTenantId, () => currentUser);
        var options = WidgetDbContextOptionsFactory.Create(interceptor);

        return new WidgetDbContext(options, () => accessor.CurrentTenantId);
    }

    [Fact]
    public void Stamps_TenantId_CreatedAt_and_CreatedBy_on_insert_without_the_caller_setting_them()
    {
        using var context = CreateContext(out var tenantAccessor);
        var tenantId = Guid.NewGuid();
        tenantAccessor.CurrentTenantId = tenantId;
        var before = DateTimeOffset.UtcNow;

        var widget = new Widget { Name = "New widget" };
        context.Widgets.Add(widget);
        context.SaveChanges();

        Assert.Equal(tenantId, widget.TenantId);
        Assert.Equal("amaka", widget.CreatedBy);
        Assert.True(widget.CreatedAt >= before);
        Assert.Null(widget.UpdatedAt);
        Assert.Null(widget.UpdatedBy);
    }

    [Fact]
    public void Stamps_UpdatedAt_and_UpdatedBy_on_modify_without_touching_CreatedAt()
    {
        using var context = CreateContext(out var tenantAccessor);
        tenantAccessor.CurrentTenantId = Guid.NewGuid();

        var widget = new Widget { Name = "New widget" };
        context.Widgets.Add(widget);
        context.SaveChanges();
        var originalCreatedAt = widget.CreatedAt;
        var originalCreatedBy = widget.CreatedBy;

        widget.Name = "Renamed widget";
        context.SaveChanges();

        Assert.Equal(originalCreatedAt, widget.CreatedAt);
        Assert.Equal(originalCreatedBy, widget.CreatedBy);
        Assert.NotNull(widget.UpdatedAt);
        Assert.Equal("amaka", widget.UpdatedBy);
    }

    [Fact]
    public void Does_not_overwrite_an_explicitly_set_TenantId_when_no_ambient_tenant_is_present()
    {
        using var context = CreateContext(out _); // ambient tenant left null throughout
        var explicitTenantId = Guid.NewGuid();

        var widget = new Widget { Name = "System-created widget", TenantId = explicitTenantId };
        context.Widgets.Add(widget);
        context.SaveChanges();

        Assert.Equal(explicitTenantId, widget.TenantId);
    }

    [Fact]
    public void Falls_back_to_system_when_no_ambient_caller_identity_is_present()
    {
        using var context = CreateContext(out var tenantAccessor, currentUser: null);
        tenantAccessor.CurrentTenantId = Guid.NewGuid();

        var widget = new Widget { Name = "New widget" };
        context.Widgets.Add(widget);
        context.SaveChanges();

        Assert.Equal("system", widget.CreatedBy);
    }
}
