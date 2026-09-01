using System.Net;
using Microsoft.AspNetCore.Http;

namespace Tiki.Shared.Logging;

/// <summary>
/// Resolves the real caller IPv4 address — the first hop of <c>X-Forwarded-For</c> when
/// the caller is behind a gateway/load balancer/reverse proxy, falling back to the
/// connection's own <c>HttpContext.Connection.RemoteIpAddress</c>
/// otherwise. An IPv6-mapped-IPv4 address (<c>::ffff:a.b.c.d</c>) is unwrapped to plain
/// IPv4 rather than logged as the IPv6 literal.
/// </summary>
public static class ClientIpAccessor
{
    public const string ForwardedForHeaderName = "X-Forwarded-For";

    public static string? Resolve(HttpContext context)
    {
        var forwardedFor = context.Request.Headers[ForwardedForHeaderName].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(forwardedFor))
        {
            // X-Forwarded-For is a comma-separated hop chain; the first entry is the
            // original caller, closest to the edge.
            var firstHop = forwardedFor.Split(',', StringSplitOptions.TrimEntries).FirstOrDefault();
            if (firstHop is not null && IPAddress.TryParse(firstHop, out var forwardedAddress))
                return Normalize(forwardedAddress);
        }

        var remoteAddress = context.Connection.RemoteIpAddress;
        return remoteAddress is null ? null : Normalize(remoteAddress);
    }

    private static string Normalize(IPAddress address)
    {
        var normalized = address.IsIPv4MappedToIPv6 ? address.MapToIPv4() : address;
        return normalized.ToString();
    }
}
