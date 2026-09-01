namespace Tiki.Shared.Core.Attributes;

/// <summary>How a <see cref="SensitiveAttribute"/>-marked property is masked before it reaches a log sink.</summary>
public enum SensitiveMaskStrategy
{
    /// <summary>Replaces the entire value with a fixed redaction marker.</summary>
    FullRedact = 0,

    /// <summary>Keeps the last four characters visible, masks everything before them — useful for card numbers, account tails.</summary>
    LastFourVisible,

    /// <summary>Replaces the value with a one-way SHA-256 hash — the same input always masks to the same output, without exposing it, so equality can still be reasoned about across log lines.</summary>
    Hashed,
}

/// <summary>
/// Marks a property as never safe to log in full. <see cref="Logging.SensitiveDataMaskingPolicy"/>
/// finds every property carrying this attribute on any type destructured for structured
/// logging and replaces its value with the masked form before the log event reaches any
/// sink — this works for any type, not just ones a developer remembers to hand-mask at the
/// log call site.
/// </summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
public sealed class SensitiveAttribute(SensitiveMaskStrategy strategy = SensitiveMaskStrategy.FullRedact) : Attribute
{
    public SensitiveMaskStrategy Strategy { get; } = strategy;
}
