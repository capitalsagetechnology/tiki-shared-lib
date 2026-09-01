using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Tiki.Shared.Persistence.Entities;

namespace Tiki.Shared.Persistence;

/// <summary>
/// EF Core model-building conventions applied once, from <c>OnModelCreating</c>, by a
/// service's own Infrastructure-layer <c>DbContext</c> — this is the one module in
/// <c>Tiki.Shared</c> allowed to reference <c>Microsoft.EntityFrameworkCore</c> directly,
/// since it is consumed only there, never from Domain or Application.
/// </summary>
public static class ModelBuilderExtensions
{
    /// <summary>
    /// Walks every entity type deriving from <see cref="BaseEntity"/> and applies two
    /// global query filters — <c>TenantId == currentTenantIdAccessor()</c> and
    /// <c>IsDeleted == false</c> — so no repository anywhere has to remember a
    /// <c>WHERE</c> clause, and cross-tenant leakage through a forgotten filter becomes
    /// structurally hard. Also indexes <c>TenantId</c> on every such entity, since every
    /// query through it is now implicitly filtered by that column.
    /// </summary>
    /// <param name="modelBuilder">The model builder from <c>OnModelCreating</c>.</param>
    /// <param name="currentTenantIdAccessor">
    /// Read once per <c>DbContext</c> instance (at that instance's first query), not once
    /// at model-build time — pass e.g. <c>() =&gt; ServiceContext.TenantId</c>. This matches
    /// how a <c>DbContext</c> is actually used: one instance per request/scope, one fixed
    /// tenant for that instance's whole lifetime — never a value that changes mid-instance,
    /// so a fresh scoped instance per request is what makes each request see its own tenant.
    /// </param>
    /// <remarks>
    /// <c>IgnoreQueryFilters()</c> is the one sanctioned escape hatch for a genuinely
    /// tenant-spanning admin/audit query — call it explicitly, at the query site, so it is
    /// visible in review. Raw SQL via <c>FromSqlRaw</c>/<c>FromSqlInterpolated</c> bypasses
    /// this filter entirely and needs its own explicit <c>WHERE "TenantId" = ...</c> clause;
    /// this convention cannot protect a query EF Core never sees.
    /// </remarks>
    public static ModelBuilder ApplyTikiConventions(this ModelBuilder modelBuilder, Func<Guid?> currentTenantIdAccessor)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (!typeof(BaseEntity).IsAssignableFrom(entityType.ClrType))
                continue;

            var entityBuilder = modelBuilder.Entity(entityType.ClrType);
            entityBuilder.HasIndex(nameof(BaseEntity.TenantId));
            entityBuilder.HasQueryFilter(BuildTenantAndSoftDeleteFilter(entityType.ClrType, currentTenantIdAccessor));
        }

        return modelBuilder;
    }

    private static LambdaExpression BuildTenantAndSoftDeleteFilter(Type clrType, Func<Guid?> currentTenantIdAccessor)
    {
        var entity = Expression.Parameter(clrType, "entity");

        var tenantId = Expression.Property(entity, nameof(BaseEntity.TenantId));
        var isDeleted = Expression.Property(entity, nameof(BaseEntity.IsDeleted));

        // A property access on a captured holder — not Expression.Invoke on a captured
        // delegate — because that's the shape EF Core's query-parameterization actually
        // recognizes as "evaluate this once per execution." Expression.Invoke on a
        // delegate constant isn't visited the same way: at least against the InMemory
        // provider it silently evaluates as if the accessor always returned null, so the
        // tenant filter matched nothing, ever.
        var accessorHolder = new TenantIdAccessorHolder(currentTenantIdAccessor);
        var currentTenantId = Expression.Property(
            Expression.Constant(accessorHolder), nameof(TenantIdAccessorHolder.CurrentTenantId));

        var tenantIdAsNullable = Expression.Convert(tenantId, typeof(Guid?));

        var tenantMatches = Expression.Equal(tenantIdAsNullable, currentTenantId);
        var notDeleted = Expression.Equal(isDeleted, Expression.Constant(false));

        var body = Expression.AndAlso(tenantMatches, notDeleted);

        return Expression.Lambda(body, entity);
    }

    private sealed class TenantIdAccessorHolder(Func<Guid?> accessor)
    {
        public Guid? CurrentTenantId => accessor();
    }
}
