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
    /// If the first is unknown, tries the second.
    /// </summary>
    public string GetLocusSymbol()
    {
        var symbol = GeneticParser.GetLocusSymbol(First.Symbol);
        if (symbol == "Unknown")
        {
            symbol = GeneticParser.GetLocusSymbol(Second.Symbol);
        }
        return symbol;
    }

    /// <summary>
    /// Checks if this locus matches another locus. 
    /// Handles underscores as wildcards.
    /// </summary>
    public bool Matches(Locus other)
    {
        if (GetLocusSymbol() != other.GetLocusSymbol()) return false;

        bool FirstMatch(Allele a1, Allele a2) => a1.Symbol == "_" || a2.Symbol == "_" || a1.Symbol == a2.Symbol;

        // Try both combinations because Aat is the same as atA
        return (FirstMatch(First, other.First) && FirstMatch(Second, other.Second)) ||
               (FirstMatch(First, other.Second) && FirstMatch(Second, other.First));
    }

    /// <summary>
    /// Combines this locus with another locus, where the other locus provides known alleles 
    /// that fill in any unknown underscores ('_') in this locus.
    /// The most dominant alleles are prioritized in the first position.
    /// </summary>
    public Locus Combine(Locus other)
    {
        var thisSymbol = GetLocusSymbol();
        var otherSymbol = other.GetLocusSymbol();
        
        // If this is unknown, we can definitely combine with anything known
        // If both are known, they must match symbols
        if (thisSymbol != "Unknown" && otherSymbol != "Unknown" && thisSymbol != otherSymbol)
            return this;

        // Collect all known alleles from both loci
        var alleles = new List<Allele> { First, Second, other.First, other.Second }
            .Where(a => !a.IsUnknown)
            .ToList();

        // Special handling for homozygous symbols in the same locus definition
        // If we have "En", "En" it should stay "En", "En"
        // But if we have "at" and "a" from different sources, they are distinct.
        // Wait, DistinctBy(a => a.Symbol) might have been too aggressive if we want homozygous.
        // Actually, if we have EnEn and we combine with enen, we should probably get Enen.
        // But if we have EnEn and we combine with nothing (or _), we should keep EnEn.
        
        // Let's refine: group by symbol and keep max 2.
        var groupedAlleles = alleles.GroupBy(a => a.Symbol)
                                    .SelectMany(g => g.Take(2))
                                    .ToList();
        
        // If no known alleles, return this (could be __ or __ + __)
        if (groupedAlleles.Count == 0) return this;

        // Sort by dominance if possible
        var definitions = GeneticParser.Definitions.FirstOrDefault(d => d.Symbol == GetLocusSymbol());
        if (definitions != null)
        {
            groupedAlleles = groupedAlleles.OrderBy(a => 
            {
                var def = definitions.Alleles.FirstOrDefault(ad => ad.Symbol == a.Symbol);
                return def?.Order ?? int.MaxValue;
            }).ThenBy(a => a.Symbol).ToList();
        }

        var resultFirst = groupedAlleles.Count > 0 ? groupedAlleles[0] : new Allele("_");
        var resultSecond = groupedAlleles.Count > 1 ? groupedAlleles[1] : new Allele("_");
        
        // Ensure that if we have more than 2, we respect the combination logic.
        // If 'other' specifically provided a homozygous pair, it should probably be respected if this was unknown.
        // But currently we just take top 2.

        // If other provided something but it was only 1 allele, and we had nothing, 
        // we keep the second as _. 
        // Example: other is "A_", we get "A_".
        // If other is "Aa", we get "Aa".
        
        return new Locus(resultFirst, resultSecond);
    }

    public override string ToString()
    {
        if (Second.Symbol == "_")
        {
            // If the first also has extras, they will be handled by First.ToString()
            // But we need to ensure we don't duplicate the underscore if we just want "A_"
            return $"{First}_";
        }
        return $"{First}{Second}";
    }

    /// <summary>
    /// Parses a locus string (e.g., "A_", "enen", "B_").
    /// </summary>
    public static Locus Parse(string input)
    {
        return GeneticParser.ParseLocus(input);
    }
}
