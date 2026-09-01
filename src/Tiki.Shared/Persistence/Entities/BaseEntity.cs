using System.ComponentModel.DataAnnotations;

namespace Tiki.Shared.Persistence.Entities;

/// <summary>
/// Base class for every tenant-scoped, soft-deletable entity across every Tiki service.
/// This file has zero package dependencies beyond the BCL — <see cref="TimestampAttribute"/>
/// lives in <c>System.ComponentModel.DataAnnotations</c>, not EF Core — so Domain-layer
/// code can inherit from it freely. Only Infrastructure, via
/// <see cref="ModelBuilderExtensions"/> and <see cref="TenantAuditSaveChangesInterceptor"/>,
/// ever references EF Core itself.
/// </summary>
public abstract class BaseEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Stamped automatically by <see cref="TenantAuditSaveChangesInterceptor"/> on insert — never set by hand.</summary>
    public Guid TenantId { get; set; }

    /// <summary>Stamped automatically on insert.</summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>Stamped automatically on insert.</summary>
    public string CreatedBy { get; set; } = string.Empty;

    /// <summary>Stamped automatically on every update after the initial insert.</summary>
    public DateTimeOffset? UpdatedAt { get; set; }

    /// <summary>Stamped automatically on every update after the initial insert.</summary>
    public string? UpdatedBy { get; set; }

    /// <summary>Soft-delete flag — excluded from every query by <see cref="ModelBuilderExtensions.ApplyTikiConventions"/>'s global filter once set.</summary>
    public bool IsDeleted { get; set; }

    /// <summary>Optimistic concurrency token.</summary>
    [Timestamp]
    public byte[] RowVersion { get; set; } = [];
}
