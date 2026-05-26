namespace RabbitGeneProcessor.Core;

/// <summary>
/// Represents a single allele at a genetic locus.
/// </summary>
public record Allele(string Symbol, List<string>? Suspected = null, List<string>? Excluded = null)
{
    /// <summary>
    /// Gets a value indicating whether this allele represents an unknown or masked value.
    /// </summary>
    public bool IsUnknown => Symbol == "_";

    public override string ToString()
    {
        var result = Symbol;
        if (Suspected is { Count: > 0 })
        {
            result += $"({string.Join("", Suspected)})";
        }
        if (Excluded is { Count: > 0 })
        {
            result += $"[{string.Join("", Excluded)}]";
        }
        return result;
    }
}
