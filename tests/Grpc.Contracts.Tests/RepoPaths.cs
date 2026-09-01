namespace Tiki.Grpc.Contracts.Tests;

/// <summary>Locates repo-relative paths from the test assembly's own output directory — no hardcoded absolute paths, works from any checkout.</summary>
internal static class RepoPaths
{
    public static string SolutionRoot { get; } = FindSolutionRoot();

    public static string ContractProjectDir(string service) =>
        Path.Combine(SolutionRoot, "src", "Grpc.Contracts", $"Tiki.Grpc.Contracts.{service}");

    public static string ContractCsproj(string service) =>
        Path.Combine(ContractProjectDir(service), $"Tiki.Grpc.Contracts.{service}.csproj");

    private static string FindSolutionRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Tiki-shared.sln")))
            dir = dir.Parent;

        return dir?.FullName
            ?? throw new InvalidOperationException(
                "Could not locate Tiki-shared.sln by walking up from the test assembly's output directory.");
    }
}
