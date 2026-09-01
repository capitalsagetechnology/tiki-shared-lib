namespace Tiki.Shared.Core.Attributes;

/// <summary>
/// Marks a controller, action, or gRPC method as requiring a valid inter-service token.
/// Enforced by <see cref="Grpc.ServiceTokenAuthInterceptor"/> for gRPC and by
/// <see cref="Auth.ServiceTokenValidationMiddleware"/> for HTTP — this attribute itself
/// carries no logic, it is the marker those components look for.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
public sealed class RequireServiceTokenAttribute : Attribute
{
    /// <summary>
    /// Optional allow-list of calling-service ids. Empty means any service holding a
    /// valid token may call — the common case, since most inter-service endpoints are
    /// not restricted to a specific caller.
    /// </summary>
    public string[] AllowedCallers { get; init; } = [];
}
