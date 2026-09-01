using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Tiki.Shared.Tests.Persistence.TestSupport;

internal static class WidgetDbContextOptionsFactory
{
    /// <summary>A fresh InMemory database with model caching disabled (see <see cref="UncachedModelCacheKeyFactory"/>) — pass interceptors to test, e.g., <c>TenantAuditSaveChangesInterceptor</c>.</summary>
    public static DbContextOptions<WidgetDbContext> Create(params IInterceptor[] interceptors)
    {
        var builder = new DbContextOptionsBuilder<WidgetDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ReplaceService<IModelCacheKeyFactory, UncachedModelCacheKeyFactory>();

        if (interceptors.Length > 0)
            builder.AddInterceptors(interceptors);

        return builder.Options;
    }
}
