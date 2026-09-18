using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Tiki.Shared.Auth;
using Tiki.Shared.Tests.Persistence.TestSupport;
using Xunit;

namespace Tiki.Shared.Tests.Persistence;

/// <summary>
/// The tenant a query filter compares against must stay a value EF reads per execution, never one
/// it writes into the model.
/// </summary>
/// <remarks>
/// This guards a live data leak. A query filter is part of the model, and the model is built once
/// per process: with the accessor overload, EF evaluated the accessor while compiling a query,
/// wrote the answer into the SQL as a literal, and cached that SQL against the query's shape —
/// so whichever tenant asked a given question first was the tenant every later asker got rows for.
/// QA reproduced it against the running platform: an operator working in one tenant was shown
/// another tenant's dispute. The mirror image was equally wrong — a shape first compiled with no
/// tenant selected froze to <c>WHERE FALSE</c> and answered nothing for anyone.
///
/// <para>
/// The behavioural proof needs a relational provider and lives with the services
/// (<c>TenantIsolationPersistenceTests</c> in Compliance, against PostgreSQL). What is checked
/// here is the property that makes it impossible: the filter the convention builds reaches the
/// tenant through the DbContext instance and holds no tenant id of its own.
/// </para>
/// </remarks>
public class TenantScopeIsNotBakedIntoTheModelTests
{
    [Fact]
    public void The_filter_reads_the_tenant_from_the_context_rather_than_holding_a_value()
    {
        var tenantId = Guid.NewGuid();

        using var context = Context(tenantId);
        var filter = FilterOf(context);

        var constants = new ConstantCollector();
        constants.Visit(filter);

        Assert.DoesNotContain(tenantId, constants.Guids);
        Assert.Contains(constants.Values, value => value is ScopedWidgetDbContext);
    }

    [Fact]
    public void Two_contexts_of_the_same_type_keep_their_own_tenants()
    {
        // The model is cached per context type, so the second context here uses a model built
        // against the first one. The filter must still answer with the second context's tenant —
        // that is exactly the case the old convention got wrong.
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();

        using var firstContext = Context(first);
        using var secondContext = Context(second);

        Assert.Equal(first, TenantIn(firstContext));
        Assert.Equal(second, TenantIn(secondContext));
    }

    [Fact]
    public async Task One_reused_context_answers_with_the_tenant_of_the_request_in_hand()
    {
        // A pooled context is handed to request after request, so the tenant cannot be captured
        // when the instance is built: the first borrower is typically a health check with no
        // tenant at all, and freezing that answers nothing for everybody who borrows it next.
        // Identity registers its context with AddPooledDbContextFactory and lost a business
        // owner's own business list to exactly this.
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();

        var previous = ServiceContext.TenantId;
        try
        {
            ServiceContext.TenantId = null;
            await using var context = AmbientContext();
            context.Widgets.AddRange(
                new Widget { Id = Guid.NewGuid(), TenantId = first, Name = "first tenant's" },
                new Widget { Id = Guid.NewGuid(), TenantId = second, Name = "second tenant's" });
            await context.SaveChangesAsync();

            ServiceContext.TenantId = first;
            var forFirst = await context.Widgets.AsNoTracking().Select(w => w.Name).ToListAsync();

            // The same instance, back from the pool, serving somebody else.
            ServiceContext.TenantId = second;
            var forSecond = await context.Widgets.AsNoTracking().Select(w => w.Name).ToListAsync();

            ServiceContext.TenantId = null;
            var forNobody = await context.Widgets.AsNoTracking().ToListAsync();

            Assert.Equal(["first tenant's"], forFirst);
            Assert.Equal(["second tenant's"], forSecond);
            Assert.Empty(forNobody);
        }
        finally
        {
            ServiceContext.TenantId = previous;
        }
    }

    private static AmbientScopedWidgetDbContext AmbientContext() =>
        new(new DbContextOptionsBuilder<AmbientScopedWidgetDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .ReplaceService<IModelCacheKeyFactory, UncachedModelCacheKeyFactory>()
            .Options);

    /// <summary>What the filter evaluates to for a given context — the value EF will parameterise.</summary>
    private static Guid TenantIn(ScopedWidgetDbContext context)
    {
        var tenantComparison = (BinaryExpression)((BinaryExpression)FilterOf(context).Body).Left;

        return (Guid)Expression.Lambda(tenantComparison.Right).Compile().DynamicInvoke()!;
    }

    /// <summary>The widget's tenant-and-soft-delete filter, as the model holds it.</summary>
    private static LambdaExpression FilterOf(ScopedWidgetDbContext context) =>
        context.Model.FindEntityType(typeof(Widget))!.GetDeclaredQueryFilters().Single().Expression;

    private static ScopedWidgetDbContext Context(Guid tenantId) =>
        new(new DbContextOptionsBuilder<ScopedWidgetDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .ReplaceService<IModelCacheKeyFactory, UncachedModelCacheKeyFactory>()
            .Options, tenantId);

    private sealed class ConstantCollector : ExpressionVisitor
    {
        public List<object?> Values { get; } = [];

        public IEnumerable<Guid> Guids => Values.OfType<Guid>();

        protected override Expression VisitConstant(ConstantExpression node)
        {
            Values.Add(node.Value);
            return base.VisitConstant(node);
        }
    }
}
