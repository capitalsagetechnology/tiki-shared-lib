using Microsoft.EntityFrameworkCore;
using Tiki.Shared.Tests.Persistence.TestSupport;
using Xunit;

namespace Tiki.Shared.Tests.Persistence;

/// <summary>Proves the global query filters actually exclude rows from a real query result — not just that the filter expression compiles.</summary>
public class ModelBuilderExtensionsTests
{
    private static WidgetDbContext CreateContext(DbContextOptions<WidgetDbContext> options, Func<Guid?> tenantIdAccessor) =>
        new(options, tenantIdAccessor);

    [Fact]
    public void Tenant_filter_excludes_another_tenant_row_from_a_query_result()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var accessor = new MutableTenantAccessor();
        Func<Guid?> tenantIdAccessor = () => accessor.CurrentTenantId;

        var options = WidgetDbContextOptionsFactory.Create();

        using (var seedContext = CreateContext(options, tenantIdAccessor))
        {
            seedContext.Widgets.AddRange(
                new Widget { Name = "Tenant A widget", TenantId = tenantA },
                new Widget { Name = "Tenant B widget", TenantId = tenantB });
            seedContext.SaveChanges();
        }

        using var context = CreateContext(options, tenantIdAccessor);
        accessor.CurrentTenantId = tenantA;

        var visible = context.Widgets.ToList();

        var widget = Assert.Single(visible);
        Assert.Equal("Tenant A widget", widget.Name);
    }

    [Fact]
    public void A_different_ambient_tenant_on_a_different_context_instance_sees_a_different_result()
    {
        // A DbContext instance corresponds to one request — one fixed tenant for its whole
        // lifetime in real usage — not a value that changes mid-instance. EF Core caches
        // the extracted filter parameter per compiled query on a given instance, so it is
        // never re-read between two calls on the SAME instance; that's expected, not a bug.
        // Two different tenants seeing two different results is proven across two
        // instances instead, each with its tenant set before its own first query.
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var accessor = new MutableTenantAccessor();
        Func<Guid?> tenantIdAccessor = () => accessor.CurrentTenantId;

        var options = WidgetDbContextOptionsFactory.Create();

        using (var seedContext = CreateContext(options, tenantIdAccessor))
        {
            seedContext.Widgets.AddRange(
                new Widget { Name = "Tenant A widget", TenantId = tenantA },
                new Widget { Name = "Tenant B widget", TenantId = tenantB });
            seedContext.SaveChanges();
        }

        accessor.CurrentTenantId = tenantA;
        using (var contextA = CreateContext(options, tenantIdAccessor))
        {
            Assert.Equal("Tenant A widget", Assert.Single(contextA.Widgets.ToList()).Name);
        }

        accessor.CurrentTenantId = tenantB;
        using (var contextB = CreateContext(options, tenantIdAccessor))
        {
            Assert.Equal("Tenant B widget", Assert.Single(contextB.Widgets.ToList()).Name);
        }
    }

    [Fact]
    public void Soft_delete_filter_excludes_a_deleted_row()
    {
        var tenantId = Guid.NewGuid();
        var accessor = new MutableTenantAccessor { CurrentTenantId = tenantId };
        Func<Guid?> tenantIdAccessor = () => accessor.CurrentTenantId;

        var options = WidgetDbContextOptionsFactory.Create();

        using (var seedContext = CreateContext(options, tenantIdAccessor))
        {
            seedContext.Widgets.AddRange(
                new Widget { Name = "Active widget", TenantId = tenantId, IsDeleted = false },
                new Widget { Name = "Deleted widget", TenantId = tenantId, IsDeleted = true });
            seedContext.SaveChanges();
        }

        using var context = CreateContext(options, tenantIdAccessor);

        var widget = Assert.Single(context.Widgets.ToList());
        Assert.Equal("Active widget", widget.Name);
    }

    [Fact]
    public void IgnoreQueryFilters_is_the_sanctioned_escape_hatch_for_a_tenant_spanning_query()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var accessor = new MutableTenantAccessor { CurrentTenantId = tenantA };
        Func<Guid?> tenantIdAccessor = () => accessor.CurrentTenantId;

        var options = WidgetDbContextOptionsFactory.Create();

        using (var seedContext = CreateContext(options, tenantIdAccessor))
        {
            seedContext.Widgets.AddRange(
                new Widget { Name = "Tenant A widget", TenantId = tenantA },
                new Widget { Name = "Tenant B widget", TenantId = tenantB });
            seedContext.SaveChanges();
        }

        using var context = CreateContext(options, tenantIdAccessor);

        var all = context.Widgets.IgnoreQueryFilters().ToList();

        Assert.Equal(2, all.Count);
    }

    [Fact]
    public void Every_BaseEntity_derived_type_gets_an_index_on_TenantId()
    {
        var options = WidgetDbContextOptionsFactory.Create();

        using var context = CreateContext(options, () => null);

        var entityType = context.Model.FindEntityType(typeof(Widget))!;
        var hasTenantIdIndex = entityType.GetIndexes()
            .Any(index => index.Properties.Count == 1 && index.Properties[0].Name == nameof(Widget.TenantId));

        Assert.True(hasTenantIdIndex);
    }
}
