namespace RabbitGeneProcessor.Core;

/// <summary>
/// Represents a single allele at a genetic locus.
/// </summary>
public record Allele(string Symbol)
{
    /// <summary>
    /// Gets a value indicating whether this allele represents an unknown or masked value.
    /// </summary>
    public bool IsUnknown => Symbol == "_";

    public override string ToString() => Symbol;
}
