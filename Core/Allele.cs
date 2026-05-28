namespace RabbitGeneProcessor.Core;

/// <summary>
/// Represents a single allele at a genetic locus.
/// </summary>
public record Allele(string Symbol, List<string>? Suspected = null, List<string>? Excluded = null, bool UseSlashInSuspected = false)
{
    /// <summary>
    /// Gets a value indicating whether this allele represents an unknown or masked value.
    /// </summary>
    public bool IsUnknown => Symbol == "_" || Symbol == "*" || Symbol == "?";

    /// <summary>
    /// Gets a value indicating whether this allele represents a preservation wildcard.
    /// </summary>
    public bool IsPreserveWildcard => Symbol == "*" || Symbol == "?";

    public override string ToString()
    {
        if (Symbol == "*" || Symbol == "?") return Symbol;
        var result = Symbol;
        if (Suspected is { Count: > 0 })
        {
            var separator = UseSlashInSuspected ? "/" : "";
            result += $"({string.Join(separator, Suspected)})";
        }
        if (Excluded is { Count: > 0 })
        {
            result += $"[{string.Join("", Excluded)}]";
        }
        return result;
    }
}
