using Microsoft.EntityFrameworkCore;
using Tiki.Shared.Auth;
using Tiki.Shared.Persistence;

namespace Tiki.Shared.Tests.Persistence.TestSupport;

/// <summary>
/// The shape a service should use when its context can be pooled: the tenant is still a member of
/// the context, but it is a property that reads the ambient tenant rather than a field captured
/// once in the constructor.
/// </summary>
/// <remarks>
/// <see cref="ScopedWidgetDbContext"/> beside this one captures in the constructor, which is
/// correct only while each instance serves exactly one request. Under
/// <c>AddPooledDbContextFactory</c> it is not: the instance outlives the request that built it.
/// </remarks>
internal sealed class AmbientScopedWidgetDbContext(DbContextOptions<AmbientScopedWidgetDbContext> options)
    : DbContext(options)
{
    public Guid CurrentTenantScope => ServiceContext.TenantId ?? Guid.Empty;

    public DbSet<Widget> Widgets => Set<Widget>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.Entity<Widget>();
        modelBuilder.ApplyTikiConventions(this, c => c.CurrentTenantScope);
    }
}
