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
    /// Read e.g. <c>() =&gt; ServiceContext.TenantId</c>.
    /// <para>
    /// <b>Superseded, and unsafe on a relational provider.</b> Use
    /// <see cref="ApplyTikiConventions{TContext}(ModelBuilder, TContext, Expression{Func{TContext, Guid}})"/>
    /// instead. A query filter is part of the model, and the model is built once per process:
    /// EF evaluates this accessor while <em>compiling</em> a query, writes the answer into the
    /// SQL as a literal, and caches that SQL against the query's shape. Whichever tenant runs a
    /// given query first therefore owns it —
    /// <c>WHERE "TenantId" = '&lt;first tenant&gt;'</c> is then served to every tenant that asks
    /// afterwards — and a shape first compiled with no tenant selected freezes to
    /// <c>WHERE FALSE</c> and answers nothing for anybody. Both were reproduced against the
    /// running platform. The overload below reads the tenant through the context instance, which
    /// is the shape EF turns into a per-execution parameter.
    /// </para>
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

    /// <summary>
    /// The same conventions, with the tenant read through the <c>DbContext</c> instance — which
    /// is what keeps one tenant's rows out of another tenant's query.
    /// </summary>
    /// <param name="modelBuilder">The model builder from <c>OnModelCreating</c>.</param>
    /// <param name="context">The context being built — pass <c>this</c>.</param>
    /// <param name="tenantSelector">
    /// The context property holding this request's tenant, e.g. <c>c =&gt; c.CurrentTenantScope</c>,
    /// assigned once in the context's constructor from <c>ServiceContext.TenantId</c>.
    /// </param>
    /// <remarks>
    /// <para>
    /// Two details carry the whole weight. <b>The value is reached through the context</b>, so EF
    /// substitutes it per execution (<c>WHERE "TenantId" = $1</c>) instead of compiling it into
    /// the cached SQL as a literal — the fault in the accessor overload above, which let the
    /// tenant that compiled a query shape first serve its rows to every tenant that asked next.
    /// <b>And the value is a non-nullable <see cref="Guid"/></b>: comparing a column to a null
    /// parameter is not "matches nothing" to EF, it is constant-folded to <c>FALSE</c> and cached
    /// that way, so use <see cref="Guid.Empty"/> for "no tenant selected" — no row carries it, and
    /// the SQL stays parameterised.
    /// </para>
    /// <para>
    /// <c>IgnoreQueryFilters()</c> remains the one sanctioned escape hatch for a genuinely
    /// tenant-spanning admin query, called at the query site so it shows up in review.
    /// </para>
    /// </remarks>
    public static ModelBuilder ApplyTikiConventions<TContext>(
        this ModelBuilder modelBuilder, TContext context, Expression<Func<TContext, Guid>> tenantSelector)
        where TContext : DbContext
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(tenantSelector);

        // The selector is written against the context type; bind it to this instance, which is
        // the reference EF recognises and rewrites into a parameter.
        var currentTenantId = new ContextInstanceRewriter(tenantSelector.Parameters[0], Expression.Constant(context, typeof(TContext)))
            .Visit(tenantSelector.Body);

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (!typeof(BaseEntity).IsAssignableFrom(entityType.ClrType) ||
                entityType.IsOwned() ||
                entityType.BaseType is not null)
            {
                continue;
            }

            var entityBuilder = modelBuilder.Entity(entityType.ClrType);
            entityBuilder.HasIndex(nameof(BaseEntity.TenantId));

            var entity = Expression.Parameter(entityType.ClrType, "entity");
            var body = Expression.AndAlso(
                Expression.Equal(Expression.Property(entity, nameof(BaseEntity.TenantId)), currentTenantId),
                Expression.Not(Expression.Property(entity, nameof(BaseEntity.IsDeleted))));

            entityBuilder.HasQueryFilter(Expression.Lambda(body, entity));
        }

        return modelBuilder;
    }

    /// <summary>Replaces the selector's context parameter with the context instance itself.</summary>
    private sealed class ContextInstanceRewriter(ParameterExpression parameter, Expression instance) : ExpressionVisitor
    {
        protected override Expression VisitParameter(ParameterExpression node) =>
            node == parameter ? instance : base.VisitParameter(node);
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
