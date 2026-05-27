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
            .DistinctBy(a => a.Symbol) // Avoid duplicates like A, A
            .ToList();

        // If no known alleles, return this (could be __ or __ + __)
        if (alleles.Count == 0) return this;

        // Sort by dominance if possible
        var definitions = GeneticParser.Definitions.FirstOrDefault(d => d.Symbol == GetLocusSymbol());
        if (definitions != null)
        {
            alleles = alleles.OrderBy(a => 
            {
                var def = definitions.Alleles.FirstOrDefault(ad => ad.Symbol == a.Symbol);
                return def?.Order ?? int.MaxValue;
            }).ToList();
        }

        // We take the top 2 known alleles. 
        // If we have more than 2, it means an override happened. 
        // Usually, the 'other' (more specific) should win if it provides specific alleles.
        // But the user said: "The first _ is filled with the most dominant known gene, the second is filled with the next known gene."
        
        // Wait, if I have A_ and I combine with at_, I should get Aat.
        // Current 'alleles' would have A and at.
        // If I have __ and combine with A_, I get A_.
        
        var resultFirst = alleles.Count > 0 ? alleles[0] : new Allele("_");
        var resultSecond = alleles.Count > 1 ? alleles[1] : new Allele("_");

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
