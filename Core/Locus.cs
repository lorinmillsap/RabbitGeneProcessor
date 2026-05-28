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
    /// If forceOverride is true, the 'other' locus alleles will replace 'this' locus alleles.
    /// </summary>
    public Locus Combine(Locus other, bool forceOverride = false)
    {
        if (forceOverride)
        {
            // Hard override: Take the alleles from 'other', but don't lose specificity 
            // if 'other' has unknowns and 'this' has knowns, UNLESS the user wants a strict override.
            // For Breed requirements, if a breed says A_, it means A must be present. 
            // If variety says aa, then A_ combined with aa should probably be A_ (as breed requirement).
            // Actually, if we return other, then Rhinelander A_ + Chocolate aa = A_. This is what user wants.
            
            // BUT for EnEn (Breed) + enen (Passive filter), we want EnEn to win.
            // If we apply Passive Filter LAST with forceOverride=false:
            // this = EnEn, other = enen.
            // Combine(enen, false):
            // Locus.Combine(enen, false):
            // if (!other.First.IsUnknown && !other.Second.IsUnknown) return other; -> Returns enen.
            // THIS IS THE PROBLEM. The non-force combine is TOO aggressive in replacing with homozygous knowns.
            
            return other; 
        }

        var thisSymbol = GetLocusSymbol();
        var otherSymbol = other.GetLocusSymbol();
        
        // If this is unknown, we can definitely combine with anything known
        // If both are known, they must match symbols
        if (thisSymbol != "Unknown" && otherSymbol != "Unknown" && thisSymbol != otherSymbol)
            return this;

        // Determine if 'other' is more specific than 'this'
        // or if we should just take 'other' if it has no underscores.
        
        // If 'other' has no unknowns, it is a complete definition.
        if (!other.First.IsUnknown && !other.Second.IsUnknown)
        {
            // FILL-IN LOGIC: If 'this' is also complete, we don't automatically overwrite 
            // if we are doing a soft combine, unless 'this' was just a default.
            // Actually, if we are here, forceOverride is false. 
            // If 'this' is EnEn and 'other' is enen, we should probably keep EnEn if it's already set?
            // Or maybe the other way around?
            
            if (!First.IsUnknown && !Second.IsUnknown)
            {
                // Both are complete. In a soft combine, keep 'this'.
                return this;
            }
            return other;
        }

        // If 'this' is completely unknown, take 'other'.
        if (First.IsUnknown && Second.IsUnknown)
        {
            return other;
        }

        // Fill in holes
        var f = First;
        var s = Second;

        if (f.IsUnknown && !other.First.IsUnknown) f = other.First;
        else if (s.IsUnknown && !other.First.IsUnknown && other.First.Symbol != f.Symbol) s = other.First;

        if (s.IsUnknown && !other.Second.IsUnknown) s = other.Second;

        // Sort by dominance
        var resultFirst = f;
        var resultSecond = s;
        
        var definitions = GeneticParser.Definitions.FirstOrDefault(d => d.Symbol == (thisSymbol != "Unknown" ? thisSymbol : otherSymbol));
        if (definitions != null && !resultFirst.IsUnknown && !resultSecond.IsUnknown && resultFirst.Symbol != resultSecond.Symbol)
        {
            var def1 = definitions.Alleles.FirstOrDefault(ad => ad.Symbol == resultFirst.Symbol);
            var def2 = definitions.Alleles.FirstOrDefault(ad => ad.Symbol == resultSecond.Symbol);
            
            if (def1 != null && def2 != null && def2.Order < def1.Order)
            {
                resultFirst = s;
                resultSecond = f;
            }
        }

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
