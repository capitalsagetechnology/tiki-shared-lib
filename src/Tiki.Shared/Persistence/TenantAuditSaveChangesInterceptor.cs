using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Tiki.Shared.Persistence.Entities;

namespace Tiki.Shared.Persistence;

/// <summary>
/// Stamps <see cref="BaseEntity.TenantId"/>, <see cref="BaseEntity.CreatedAt"/>, and
/// <see cref="BaseEntity.CreatedBy"/> on every newly-<see cref="EntityState.Added"/>
/// entity, and <see cref="BaseEntity.UpdatedAt"/>/<see cref="BaseEntity.UpdatedBy"/> on
/// every <see cref="EntityState.Modified"/> one — reading tenant and caller identity from
/// the same ambient accessors a service passes to
/// <see cref="ModelBuilderExtensions.ApplyTikiConventions"/>, so no service code ever sets
/// these fields by hand.
/// </summary>
public sealed class TenantAuditSaveChangesInterceptor(
    Func<Guid?> currentTenantIdAccessor,
    Func<string?> currentUserAccessor) : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Stamp(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        Stamp(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void Stamp(DbContext? context)
    {
        if (context is null)
            return;

        var now = DateTimeOffset.UtcNow;
        var currentTenantId = currentTenantIdAccessor();
        var currentUser = currentUserAccessor() ?? "system";

        foreach (var entry in context.ChangeTracker.Entries<BaseEntity>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    if (currentTenantId is not null)
                        entry.Entity.TenantId = currentTenantId.Value;

                    entry.Entity.CreatedAt = now;
                    entry.Entity.CreatedBy = currentUser;
                    break;

                case EntityState.Modified:
                    entry.Entity.UpdatedAt = now;
                    entry.Entity.UpdatedBy = currentUser;
                    break;
            }
        }
    }
}
