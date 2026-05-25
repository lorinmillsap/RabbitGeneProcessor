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

    public override string ToString() => $"{First}{Second}";

    /// <summary>
    /// Parses a locus string (e.g., "A_", "enen", "B_").
    /// </summary>
    public static Locus Parse(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            throw new ArgumentException("Locus string cannot be empty.", nameof(input));

        // Basic parsing: split into two alleles. 
        // For rabbit genetics, some loci are 2 characters (e.g., "en"), some 1 (e.g., "A").
        // Comma separation handles the split between loci, but within a locus we need to be careful.
        
        // Simple heuristic for now: 
        // If length is 2: each character is an allele.
        // If length is 4 (e.g. enen): first two and last two are alleles.
        // If length is 3 (e.g. En_): first two and last one? No, usually it's "Enen" or "enen" or "En_".
        
        // Let's refine based on the example: "A_,B_,C_,D_,E,enen"
        // Wait, the example says "E" is a single letter but it's a locus. 
        // Actually, the example says "A_,B_,C_D_E,enen" in the first prompt, 
        // and "A_,B_,C_,D_,E,enen" in the second.
        
        // If it's a single letter like 'E', it's likely a dominant 'E' with an unknown second allele 'E_'.
        // Or maybe 'E' means both are 'E'?
        
        if (input.Length == 1)
        {
             return new Locus(new Allele(input), new Allele("_"));
        }
        
        if (input.Length == 2)
        {
            return new Locus(new Allele(input[0].ToString()), new Allele(input[1].ToString()));
        }
        
        if (input.Length == 4)
        {
            // Case for enen
            return new Locus(new Allele(input.Substring(0, 2)), new Allele(input.Substring(2, 2)));
        }

        // Fallback or more complex cases
        throw new NotSupportedException($"Unsupported locus format: {input}");
    }
}
