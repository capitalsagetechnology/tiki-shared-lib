using Microsoft.EntityFrameworkCore;
using Tiki.Shared.Persistence;

namespace Tiki.Shared.Tests.Persistence.TestSupport;

/// <summary>
/// A context scoped the way a service should scope one: the tenant is a member of the context,
/// read by the query filter through <c>this</c>.
/// </summary>
internal sealed class ScopedWidgetDbContext(DbContextOptions<ScopedWidgetDbContext> options, Guid tenantId)
    : DbContext(options)
{
    public Guid CurrentTenantScope { get; } = tenantId;

    public DbSet<Widget> Widgets => Set<Widget>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.Entity<Widget>();
        modelBuilder.ApplyTikiConventions(this, c => c.CurrentTenantScope);
    }
}
