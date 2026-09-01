using Tiki.Shared.Persistence.Entities;

namespace Tiki.Shared.Tests.Persistence.TestSupport;

internal sealed class Widget : BaseEntity
{
    public string Name { get; set; } = string.Empty;
}
