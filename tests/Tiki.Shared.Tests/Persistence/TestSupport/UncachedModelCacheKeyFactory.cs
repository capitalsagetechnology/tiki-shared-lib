using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Tiki.Shared.Tests.Persistence.TestSupport;

/// <summary>
/// EF Core caches a DbContext's model per CLR type by default. Every test in this project
/// shares the <see cref="WidgetDbContext"/> type but constructs its own query-filter
/// closure over its own <see cref="MutableTenantAccessor"/> — without this, whichever test
/// happens to build the model first would have its closure silently reused by every other
/// test in the same run. Production code never hits this: a real service registers exactly
/// one <c>DbContext</c> type with exactly one consistent accessor for the app's lifetime.
/// </summary>
internal sealed class UncachedModelCacheKeyFactory : IModelCacheKeyFactory
{
    public object Create(DbContext context, bool designTime) => new object();
}
