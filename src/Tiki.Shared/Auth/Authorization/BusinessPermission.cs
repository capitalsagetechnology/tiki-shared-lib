using System.Globalization;

namespace Tiki.Shared.Auth.Authorization;

/// <summary>
/// One cell of a business's own permission grid — a <see cref="BusinessModule"/> paired with a
/// <see cref="PermissionAction"/> — and the canonical <c>business:{module}:{action}</c> string
/// it is stored and compared as.
/// </summary>
/// <remarks>
/// The <c>business:</c> prefix keeps these strings from ever colliding with a Tiki-staff
/// <see cref="TikiPermission"/> in the same permission list — a business team member's session
/// carries both vocabularies folded into one string set (see
/// <see cref="Sessions.TikiSession.HasBusinessPermission"/>), and a bare <c>"roles:write"</c>
/// would otherwise mean two different things depending on which enum produced it.
/// </remarks>
public readonly record struct BusinessPermission(BusinessModule Module, PermissionAction Action)
{
    /// <summary>The wire form, e.g. <c>business:store:write</c>.</summary>
    public string Value => Format(Module, Action);

    public static string Format(BusinessModule module, PermissionAction action) =>
        string.Create(CultureInfo.InvariantCulture, $"business:{module.ToString().ToLowerInvariant()}:{action.ToString().ToLowerInvariant()}");

    public static bool TryParse(string value, out BusinessPermission permission)
    {
        permission = default;

        const string prefix = "business:";
        if (!value.StartsWith(prefix, StringComparison.Ordinal))
            return false;

        var rest = value[prefix.Length..];
        var separator = rest.IndexOf(':', StringComparison.Ordinal);
        if (separator <= 0 || separator == rest.Length - 1)
            return false;

        if (!Enum.TryParse<BusinessModule>(rest[..separator], ignoreCase: true, out var module) ||
            !Enum.TryParse<PermissionAction>(rest[(separator + 1)..], ignoreCase: true, out var action))
        {
            return false;
        }

        permission = new BusinessPermission(module, action);
        return true;
    }

    /// <summary>
    /// Expands a granted set into everything it implies: a <c>Write</c> grant also yields the
    /// matching <c>Read</c>. Mirrors <see cref="TikiPermission.Expand"/> exactly — same
    /// implication rule, same "materialise once, at grant time" reasoning.
    /// </summary>
    public static IReadOnlyList<string> Expand(IEnumerable<BusinessPermission> granted)
    {
        var expanded = new HashSet<string>(StringComparer.Ordinal);

        foreach (var permission in granted)
        {
            expanded.Add(permission.Value);

            if (permission.Action == PermissionAction.Write)
                expanded.Add(Format(permission.Module, PermissionAction.Read));
        }

        return [.. expanded];
    }

    public override string ToString() => Value;
}
