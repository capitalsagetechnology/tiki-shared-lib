namespace Tiki.Shared.Tests.Persistence.TestSupport;

/// <summary>A settable box standing in for whatever ambient tenant source a real service would pass — lets one test vary the "current tenant" between queries without creating a new DbContext (and risking EF Core's per-type model cache reusing a different test's closure).</summary>
internal sealed class MutableTenantAccessor
{
    public Guid? CurrentTenantId { get; set; }
}
