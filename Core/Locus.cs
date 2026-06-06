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
        if (OverrideLocusSymbol != null) return OverrideLocusSymbol;
        var symbol = GeneticParser.GetLocusSymbol(First.Symbol);
        if (symbol == "Unknown")
        {
            symbol = GeneticParser.GetLocusSymbol(Second.Symbol);
        }
        return symbol;
    }

    /// <summary>
    /// Gets or sets an optional locus symbol that overrides detection.
    /// </summary>
    public string? OverrideLocusSymbol { get; set; }

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
    /// Normalizes and sorts the locus based on dominance rules.
    /// Dominant alleles always come first. 
    /// Dominant genes are always dominant, so what the recessive is doesn't matter for normalization order.
    /// Partially dominant/recessive (stacking) genes dominate based on their order of dominance.
    /// </summary>
    public Locus Normalize()
    {
        if (First.IsUnknown && Second.IsUnknown) return this;
        if (First.IsUnknown && !Second.IsUnknown) return new Locus(Second, First);
        if (Second.IsUnknown) return this;

        var def1 = First.GetDefinition();
        var def2 = Second.GetDefinition();

        if (def1 == null || def2 == null) return this;

        // All alleles work the same: sort by Order of dominance.
        // dominant, partially dominant, partially recessive, recessive
        if (def2.Order < def1.Order)
        {
            return new Locus(Second, First);
        }

        return this;
    }

    /// <summary>
    /// Combines this locus with another locus, where the other locus provides known alleles 
    /// that fill in any unknown underscores ('_') in this locus.
    /// </summary>
    public Locus Combine(Locus other, bool forceOverride = false)
    {
        if (forceOverride)
        {
            var resF = other.First.IsPreserveWildcard ? First : other.First;
            var resS = other.Second.IsPreserveWildcard ? Second : other.Second;
            return new Locus(resF, resS).Normalize();
        }

        var thisSymbol = GetLocusSymbol();
        var otherSymbol = other.GetLocusSymbol();
        
        if (thisSymbol != "Unknown" && otherSymbol != "Unknown" && thisSymbol != otherSymbol)
            return this;

        var f = First;
        var s = Second;

        var otherF = other.First;
        var otherS = other.Second;

        // Fill in unknowns
        if (f.IsUnknown && !otherF.IsUnknown && !otherF.IsPreserveWildcard) f = otherF;
        else if (s.IsUnknown && !otherF.IsUnknown && !otherF.IsPreserveWildcard && otherF.Symbol != f.Symbol) s = otherF;

        if (s.IsUnknown && !otherS.IsUnknown && !otherS.IsPreserveWildcard && otherS.Symbol != f.Symbol) s = otherS;

        return new Locus(f, s).Normalize();
    }

    public override string ToString()
    {
        if (Second.Symbol == "_")
        {
            // Special handling for suspected/excluded alleles on an unknown recessive
            if (Second.Suspected is { Count: > 0 } || Second.Excluded is { Count: > 0 })
            {
                return $"{First}{Second}";
            }
            // If the first allele has suspects/exclusions, we should also show them clearly
            if (First.Suspected is { Count: > 0 } || First.Excluded is { Count: > 0 })
            {
                return $"{First}{Second}";
            }
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
