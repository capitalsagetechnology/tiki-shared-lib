using System.Globalization;

namespace Tiki.Shared.Auth.Authorization;

/// <summary>
/// One cell of the permission grid — a module paired with an action — and the canonical
/// <c>{module}:{action}</c> string it is stored and compared as.
/// </summary>
/// <remarks>
/// The string form is what lands on a session in Redis and travels between services, so it is
/// produced in exactly one place. Every comparison is ordinal and lower-case; a permission
/// check that varied by culture would behave differently in a Turkish locale, where
/// <c>I</c>.ToLower() is not <c>i</c>.
/// </remarks>
public readonly record struct TikiPermission(TikiModule Module, PermissionAction Action)
{
    /// <summary>The wire form, e.g. <c>wallets:read</c>.</summary>
    public string Value => Format(Module, Action);

    public static string Format(TikiModule module, PermissionAction action) =>
        string.Create(CultureInfo.InvariantCulture, $"{module.ToString().ToLowerInvariant()}:{action.ToString().ToLowerInvariant()}");

    public static bool TryParse(string value, out TikiPermission permission)
    {
        permission = default;

        var separator = value.IndexOf(':', StringComparison.Ordinal);
        if (separator <= 0 || separator == value.Length - 1)
            return false;

        if (!Enum.TryParse<TikiModule>(value[..separator], ignoreCase: true, out var module) ||
            !Enum.TryParse<PermissionAction>(value[(separator + 1)..], ignoreCase: true, out var action))
        {
            return false;
        }

        permission = new TikiPermission(module, action);
        return true;
    }

    /// <summary>
    /// Expands a granted set into everything it implies: a <c>Write</c> grant also yields the
    /// matching <c>Read</c>.
    /// </summary>
    /// <remarks>
    /// Done once, at grant time, rather than at every check. Expanding on read would mean every
    /// permission comparison in the platform carries an implication rule, and the day someone
    /// writes a check that forgets it, a user with write access is denied read access.
    /// Materialising it means a stored session is already the complete answer.
    /// </remarks>
    public static IReadOnlyList<string> Expand(IEnumerable<TikiPermission> granted)
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
