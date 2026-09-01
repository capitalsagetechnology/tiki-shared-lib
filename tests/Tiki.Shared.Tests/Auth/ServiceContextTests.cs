using Tiki.Shared.Auth;
using Xunit;

namespace Tiki.Shared.Tests.Auth;

public class ServiceContextTests
{
    [Fact]
    public void BeginScope_sets_ambient_values_for_the_scope()
    {
        using (ServiceContext.BeginScope("trace-1", "wallet-service"))
        {
            Assert.Equal("trace-1", ServiceContext.TraceId);
            Assert.Equal("wallet-service", ServiceContext.CallingService);
        }
    }

    [Fact]
    public void Disposing_the_scope_restores_the_previous_values()
    {
        using (ServiceContext.BeginScope("outer-trace", "identity-service"))
        {
            using (ServiceContext.BeginScope("inner-trace", "transaction-service"))
            {
                Assert.Equal("inner-trace", ServiceContext.TraceId);
                Assert.Equal("transaction-service", ServiceContext.CallingService);
            }

            Assert.Equal("outer-trace", ServiceContext.TraceId);
            Assert.Equal("identity-service", ServiceContext.CallingService);
        }
    }

    [Fact]
    public async Task Ambient_values_flow_through_async_continuations()
    {
        using (ServiceContext.BeginScope("async-trace", "compliance-service"))
        {
            await Task.Yield();
            await Task.Delay(1);

            Assert.Equal("async-trace", ServiceContext.TraceId);
            Assert.Equal("compliance-service", ServiceContext.CallingService);
        }
    }

    [Fact]
    public void TraceId_has_a_safe_default_when_never_set()
    {
        // Runs on a brand-new async flow context (xunit isolates each [Fact] on its own
        // logical call context), so no ambient value from another test can leak in here.
        Assert.False(string.IsNullOrWhiteSpace(ServiceContext.TraceId));
    }

    [Fact]
    public void TenantId_is_null_until_explicitly_set()
    {
        Assert.Null(ServiceContext.TenantId);
    }

    [Fact]
    public void TenantId_round_trips_through_a_direct_set()
    {
        var tenantId = Guid.NewGuid();
        ServiceContext.TenantId = tenantId;

        Assert.Equal(tenantId, ServiceContext.TenantId);
    }

    [Fact]
    public void SessionId_is_null_until_explicitly_set()
    {
        Assert.Null(ServiceContext.SessionId);
    }

    [Fact]
    public void SessionId_round_trips_through_a_direct_set()
    {
        var sessionId = Guid.NewGuid();
        ServiceContext.SessionId = sessionId;

        Assert.Equal(sessionId, ServiceContext.SessionId);
    }
}
