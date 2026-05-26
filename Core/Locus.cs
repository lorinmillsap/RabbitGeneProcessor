namespace RabbitGeneProcessor.Core;

/// <summary>
/// Represents a genetic locus consisting of a pair of alleles.
/// </summary>
public record Locus(Allele First, Allele Second)
{
    /// <summary>
    /// Returns the possible alleles this locus can pass to offspring.
    /// </summary>
    public IEnumerable<Allele> GetPossibleGametes()
    {
        yield return First;
        yield return Second;
    }

    /// <summary>
    /// Returns the symbol of the locus based on the first allele's symbol.
    /// </summary>
    public string GetLocusSymbol()
    {
        return GeneticParser.GetLocusSymbol(First.Symbol);
    }

    public override string ToString() => $"{First}{Second}";

    /// <summary>
    /// Parses a locus string (e.g., "A_", "enen", "B_").
    /// </summary>
    public static Locus Parse(string input)
    {
        return GeneticParser.ParseLocus(input);
    }
}
