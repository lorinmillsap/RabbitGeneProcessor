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

    /// <summary>
    /// Gets the name of the variety based on the allele pair.
    /// </summary>
    public string? VarietyName { get; init; }

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

    /// <summary>
    /// Gets the dominance definition for this allele.
    /// </summary>
    public AlleleDefinition? GetDefinition()
    {
        var locusSymbol = GeneticParser.GetLocusSymbol(Symbol);
        var locusDef = GeneticParser.Definitions.FirstOrDefault(d => d.Symbol == locusSymbol);
        return locusDef?.Alleles.FirstOrDefault(a => a.Symbol == Symbol);
    }
}
