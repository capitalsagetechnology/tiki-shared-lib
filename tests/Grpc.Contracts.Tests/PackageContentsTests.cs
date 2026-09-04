using System.Diagnostics;
using System.IO.Compression;
using System.Xml.Linq;
using Xunit;

namespace Tiki.Grpc.Contracts.Tests;

/// <summary>
/// Packs each contract project and inspects the resulting <c>.nupkg</c> directly — proving
/// what actually ships, not just what the <c>.csproj</c> source says should ship.
/// Consumer-side drift tests (does a contract still match what a service expects) belong
/// in the consuming service's own repo, not here — see <c>tools/hash-grpc-contract.sh</c>.
/// </summary>
public sealed class PackageContentsTests : IAsyncLifetime
{
    private static readonly string[] Services = ["Identity", "Wallet", "Transaction", "Compliance", "Integration"];

    private readonly string _packOutputDir =
        Path.Combine(Path.GetTempPath(), "tiki-grpc-contracts-pack-" + Guid.NewGuid().ToString("n"));

    private readonly Dictionary<string, string> _nupkgPathByService = new();

    public async Task InitializeAsync()
    {
        Directory.CreateDirectory(_packOutputDir);

        foreach (var service in Services)
        {
            var csproj = RepoPaths.ContractCsproj(service);
            var exitCode = await RunDotnetAsync(["pack", csproj, "-c", "Release", "-o", _packOutputDir, "--nologo", "-v", "quiet"]);
            Assert.True(exitCode == 0, $"'dotnet pack' failed for {service}.");

            var nupkg = Directory.GetFiles(_packOutputDir, $"Tiki.Grpc.Contracts.{service}.*.nupkg")
                .OrderByDescending(File.GetLastWriteTimeUtc)
                .FirstOrDefault();
            Assert.False(string.IsNullOrEmpty(nupkg), $"No .nupkg produced for {service}.");
            _nupkgPathByService[service] = nupkg!;
        }
    }

    public Task DisposeAsync()
    {
        if (Directory.Exists(_packOutputDir))
            Directory.Delete(_packOutputDir, recursive: true);

        return Task.CompletedTask;
    }

    public static IEnumerable<object[]> ServiceNames => Services.Select(s => new object[] { s });

    [Theory]
    [MemberData(nameof(ServiceNames))]
    public void Package_ships_the_proto_as_plain_content_not_contentFiles(string service)
    {
        using var archive = ZipFile.OpenRead(_nupkgPathByService[service]);

        foreach (var protoFileName in PackedProtoFileNames(service))
        {
            Assert.Contains(archive.Entries, e => e.FullName == $"protos/{protoFileName}.proto");
        }

        Assert.DoesNotContain(archive.Entries, e => e.FullName.StartsWith("contentFiles/", StringComparison.Ordinal));
    }

    [Fact]
    public void Integration_package_ships_every_provider_proto()
    {
        using var archive = ZipFile.OpenRead(_nupkgPathByService["Integration"]);
        var protoEntries = archive.Entries
            .Where(e => e.FullName.StartsWith("protos/", StringComparison.Ordinal) && e.FullName.EndsWith(".proto", StringComparison.Ordinal))
            .Select(e => e.FullName["protos/".Length..])
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(["smartcomply.proto", "veriff.proto", "volume.proto"], protoEntries);
    }

    [Theory]
    [MemberData(nameof(ServiceNames))]
    public void Package_ships_the_compiled_assembly(string service)
    {
        using var archive = ZipFile.OpenRead(_nupkgPathByService[service]);

        Assert.Contains(archive.Entries, e => e.FullName == $"lib/net10.0/Tiki.Grpc.Contracts.{service}.dll");
    }

    [Theory]
    [MemberData(nameof(ServiceNames))]
    public void Package_declares_only_protobuf_and_grpc_core_api_as_dependencies(string service)
    {
        using var archive = ZipFile.OpenRead(_nupkgPathByService[service]);
        var nuspecEntry = archive.Entries.Single(e => e.FullName == $"Tiki.Grpc.Contracts.{service}.nuspec");

        using var stream = nuspecEntry.Open();
        var nuspec = XDocument.Load(stream);
        var ns = nuspec.Root!.GetDefaultNamespace();

        var dependencyIds = nuspec.Descendants(ns + "dependency")
            .Select(d => d.Attribute("id")!.Value)
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(new[] { "Google.Protobuf", "Grpc.Core.Api" }, dependencyIds);
    }

    [Theory]
    [MemberData(nameof(ServiceNames))]
    public void Package_id_matches_the_project_name(string service)
    {
        using var archive = ZipFile.OpenRead(_nupkgPathByService[service]);
        var nuspecEntry = archive.Entries.Single(e => e.FullName == $"Tiki.Grpc.Contracts.{service}.nuspec");

        using var stream = nuspecEntry.Open();
        var nuspec = XDocument.Load(stream);
        var ns = nuspec.Root!.GetDefaultNamespace();

        var id = nuspec.Descendants(ns + "id").Single().Value;

        Assert.Equal($"Tiki.Grpc.Contracts.{service}", id);
    }

    private static IReadOnlyList<string> PackedProtoFileNames(string service) => service switch
    {
        // Integration is one owning package with one proto per provider, not one
        // catch-all integration.proto.
        "Integration" => ["veriff", "volume", "smartcomply"],
        _ => [service.ToLowerInvariant()],
    };

    private static async Task<int> RunDotnetAsync(IEnumerable<string> arguments)
    {
        var startInfo = new ProcessStartInfo("dotnet")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (var argument in arguments)
            startInfo.ArgumentList.Add(argument);

        using var process = Process.Start(startInfo)!;
        await process.WaitForExitAsync();
        return process.ExitCode;
    }
}
