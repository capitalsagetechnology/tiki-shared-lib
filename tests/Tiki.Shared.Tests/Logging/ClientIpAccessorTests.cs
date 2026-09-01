using System.Net;
using Microsoft.AspNetCore.Http;
using Tiki.Shared.Logging;
using Xunit;

namespace Tiki.Shared.Tests.Logging;

public class ClientIpAccessorTests
{
    [Fact]
    public void Falls_back_to_RemoteIpAddress_when_no_forwarded_header_is_present()
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = IPAddress.Parse("203.0.113.7");

        Assert.Equal("203.0.113.7", ClientIpAccessor.Resolve(context));
    }

    [Fact]
    public void Prefers_the_first_hop_of_X_Forwarded_For_over_RemoteIpAddress()
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = IPAddress.Parse("10.0.0.1"); // the gateway, not the caller
        context.Request.Headers[ClientIpAccessor.ForwardedForHeaderName] = "198.51.100.23, 10.0.0.5";

        Assert.Equal("198.51.100.23", ClientIpAccessor.Resolve(context));
    }

    [Fact]
    public void Unwraps_an_IPv6_mapped_IPv4_RemoteIpAddress_to_plain_IPv4()
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = IPAddress.Parse("::ffff:203.0.113.5");

        Assert.Equal("203.0.113.5", ClientIpAccessor.Resolve(context));
    }

    [Fact]
    public void Unwraps_an_IPv6_mapped_IPv4_forwarded_address_too()
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = IPAddress.Parse("10.0.0.1");
        context.Request.Headers[ClientIpAccessor.ForwardedForHeaderName] = "::ffff:198.51.100.23";

        Assert.Equal("198.51.100.23", ClientIpAccessor.Resolve(context));
    }

    [Fact]
    public void Falls_back_to_RemoteIpAddress_when_the_forwarded_header_is_unparseable()
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = IPAddress.Parse("203.0.113.7");
        context.Request.Headers[ClientIpAccessor.ForwardedForHeaderName] = "not-an-ip-address";

        Assert.Equal("203.0.113.7", ClientIpAccessor.Resolve(context));
    }

    [Fact]
    public void Returns_null_when_neither_source_is_available()
    {
        var context = new DefaultHttpContext();

        Assert.Null(ClientIpAccessor.Resolve(context));
    }
}
