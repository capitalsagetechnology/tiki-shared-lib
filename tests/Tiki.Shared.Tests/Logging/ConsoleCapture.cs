using Xunit;

namespace Tiki.Shared.Tests.Logging;

/// <summary>Tests that swap <see cref="Console.Out"/> must not run alongside each other.</summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class ConsoleCaptureCollection
{
    public const string Name = "console capture";
}

/// <summary>Redirects <see cref="Console.Out"/> for the lifetime of the instance.</summary>
internal sealed class ConsoleCapture : IDisposable
{
    private readonly TextWriter _original = Console.Out;
    private readonly StringWriter _writer = new();

    public ConsoleCapture() => Console.SetOut(_writer);

    public string[] Lines =>
        _writer.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    public void Dispose() => Console.SetOut(_original);
}
