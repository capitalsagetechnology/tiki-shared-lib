using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Moq;
using Tiki.Shared.Caching;
using Tiki.Shared.Extensions;
using Xunit;

namespace Tiki.Shared.Tests.Caching;

public class TieredCacheTests
{
    private static TieredCacheOptions CacheOptions => new()
    {
        ServiceName = "wallet-service",
        DefaultL1Ttl = TimeSpan.FromMinutes(1),
        DefaultL2Ttl = TimeSpan.FromMinutes(10),
    };

    private static TieredCache CreateSut(IMemoryCache l1, Mock<IDistributedCache> l2Mock) =>
        new(l1, l2Mock.Object, Options.Create(CacheOptions));

    [Fact]
    public async Task Full_miss_invokes_factory_once_and_writes_through_both_tiers()
    {
        using var l1 = new MemoryCache(new MemoryCacheOptions());
        var l2 = new Mock<IDistributedCache>();
        l2.Setup(c => c.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((byte[]?)null);

        var sut = CreateSut(l1, l2);
        var factoryCalls = 0;

        var value = await sut.GetOrSetAsync("wallet:1:v1", _ =>
        {
            factoryCalls++;
            return Task.FromResult(42);
        });

        Assert.Equal(42, value);
        Assert.Equal(1, factoryCalls);
        l2.Verify(c => c.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
        l2.Verify(
            c => c.SetAsync(It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<DistributedCacheEntryOptions>(), It.IsAny<CancellationToken>()),
            Times.Once);
        Assert.True(l1.TryGetValue("wallet-service:wallet:1:v1", out int cached));
        Assert.Equal(42, cached);
    }

    [Fact]
    public async Task L1_hit_never_touches_L2_or_the_factory()
    {
        using var l1 = new MemoryCache(new MemoryCacheOptions());
        l1.Set("wallet-service:wallet:1:v1", 7, TimeSpan.FromMinutes(1));

        var l2 = new Mock<IDistributedCache>(MockBehavior.Strict);
        var sut = CreateSut(l1, l2);
        var factoryCalls = 0;

        var value = await sut.GetOrSetAsync("wallet:1:v1", _ =>
        {
            factoryCalls++;
            return Task.FromResult(-1);
        });

        Assert.Equal(7, value);
        Assert.Equal(0, factoryCalls);
        l2.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task L2_hit_backfills_L1_and_skips_the_factory()
    {
        using var l1 = new MemoryCache(new MemoryCacheOptions());
        var bytes = JsonSerializer.SerializeToUtf8Bytes(99, TikiJson.Options);

        var l2 = new Mock<IDistributedCache>();
        l2.Setup(c => c.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(bytes);

        var sut = CreateSut(l1, l2);
        var factoryCalls = 0;

        var value = await sut.GetOrSetAsync("wallet:1:v1", _ =>
        {
            factoryCalls++;
            return Task.FromResult(-1);
        });

        Assert.Equal(99, value);
        Assert.Equal(0, factoryCalls);
        Assert.True(l1.TryGetValue("wallet-service:wallet:1:v1", out int cached));
        Assert.Equal(99, cached);
        l2.Verify(
            c => c.SetAsync(It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<DistributedCacheEntryOptions>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task SkipL1_option_never_populates_L1()
    {
        using var l1 = new MemoryCache(new MemoryCacheOptions());
        var l2 = new Mock<IDistributedCache>();
        l2.Setup(c => c.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((byte[]?)null);

        var sut = CreateSut(l1, l2);
        var skipL1 = TieredCacheEntryOptions.SkipL1(CacheOptions);

        await sut.GetOrSetAsync("wallet:1:v1", _ => Task.FromResult(1), skipL1);

        Assert.False(l1.TryGetValue("wallet-service:wallet:1:v1", out _));
    }

    [Fact]
    public async Task InvalidateAsync_removes_from_both_tiers()
    {
        using var l1 = new MemoryCache(new MemoryCacheOptions());
        l1.Set("wallet-service:wallet:1:v1", 1);

        var l2 = new Mock<IDistributedCache>();
        var sut = CreateSut(l1, l2);

        await sut.InvalidateAsync("wallet:1:v1");

        Assert.False(l1.TryGetValue("wallet-service:wallet:1:v1", out _));
        l2.Verify(c => c.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}
