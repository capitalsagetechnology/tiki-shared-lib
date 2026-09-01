using Microsoft.EntityFrameworkCore;
using Tiki.Shared.Persistence;

namespace Tiki.Shared.Tests.Persistence.TestSupport;

internal sealed class WidgetDbContext(DbContextOptions<WidgetDbContext> options, Func<Guid?> currentTenantIdAccessor)
    : DbContext(options)
{
    public DbSet<Widget> Widgets => Set<Widget>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.Entity<Widget>();
        modelBuilder.ApplyTikiConventions(currentTenantIdAccessor);
    }
}
