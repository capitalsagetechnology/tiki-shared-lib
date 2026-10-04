using Tiki.Shared.Telemetry;
using Xunit;

namespace Tiki.Shared.Tests.Telemetry;

public class TikiTraceIdsTests
{
    [Theory]
    [InlineData("00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01", "4bf92f3577b34da6a3ce929d0e0e4736")]
    [InlineData("4BF92F3577B34DA6A3CE929D0E0E4736", "4bf92f3577b34da6a3ce929d0e0e4736")]
    [InlineData("caller-correlation-42", "caller-correlation-42")]
    [InlineData("", null)]
    [InlineData(null, null)]
    public void Normalises_to_the_32_hex_trace_id(string? raw, string? expected) =>
        Assert.Equal(expected, TikiTraceIds.Normalize(raw));
}
