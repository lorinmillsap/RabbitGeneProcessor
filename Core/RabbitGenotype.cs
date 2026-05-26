namespace RabbitGeneProcessor.Core;

/// <summary>
/// Represents the full genotype of a rabbit.
/// </summary>
public class RabbitGenotype
{
    public List<Locus> Loci { get; } = new();

    /// <summary>
    /// Parses a genotype string (e.g., "A_,B_,C_,D_,E,enen").
    /// </summary>
    public static RabbitGenotype Parse(string input)
    {
        var genotype = new RabbitGenotype();
        var parts = input.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var part in parts)
        {
            genotype.Loci.Add(Locus.Parse(part));
        }
        return genotype;
    }

    /// <summary>
    /// Calculates all possible offspring genotypes from two parents.
    /// </summary>
    public static IEnumerable<RabbitGenotype> GetOffspring(RabbitGenotype parent1, RabbitGenotype parent2)
    {
        if (parent1.Loci.Count != parent2.Loci.Count)
            throw new ArgumentException("Parents must have the same number of loci.");

        // We need to perform a Cartesian product of all possible gamete combinations for each locus.
        // For simplicity, let's just return a list of possible genotypes (ignoring duplicates for now).
        
        var results = new List<RabbitGenotype>();
        GenerateCombinations(0, new List<Locus>(), parent1, parent2, results);
        return results;
    }

    private static void GenerateCombinations(int locusIndex, List<Locus> currentLoci, RabbitGenotype p1, RabbitGenotype p2, List<RabbitGenotype> results)
    {
        if (locusIndex == p1.Loci.Count)
        {
            var genotype = new RabbitGenotype();
            genotype.Loci.AddRange(currentLoci);
            results.Add(genotype);
            return;
        }

        var p1Locus = p1.Loci[locusIndex];
        var p2Locus = p2.Loci[locusIndex];

        var p1Gametes = p1Locus.GetPossibleGametes();
        var p2Gametes = p2Locus.GetPossibleGametes();

        foreach (var g1 in p1Gametes)
        {
            foreach (var g2 in p2Gametes)
            {
                var newLocus = new Locus(g1, g2);
                currentLoci.Add(newLocus);
                GenerateCombinations(locusIndex + 1, currentLoci, p1, p2, results);
                currentLoci.RemoveAt(currentLoci.Count - 1);
            }
        }
    }

    public override string ToString() => string.Join(",", Loci);

    /// <summary>
    /// Checks if this genotype matches another genotype.
    /// Handles wildcards in both.
    /// </summary>
    public bool Matches(RabbitGenotype other)
    {
        var thisLoci = Loci.ToDictionary(l => l.GetLocusSymbol());
        var otherLoci = other.Loci.ToDictionary(l => l.GetLocusSymbol());

        var allSymbols = thisLoci.Keys.Union(otherLoci.Keys);

        foreach (var symbol in allSymbols)
        {
            if (thisLoci.TryGetValue(symbol, out var l1) && otherLoci.TryGetValue(symbol, out var l2))
            {
                if (!l1.Matches(l2)) return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Checks if this genotype "contains" the other genotype.
    /// This is stricter than Match: the other's alleles must be present in this genotype,
    /// and wildcards in the other do NOT match specific alleles in this unless they are also wildcards?
    /// Actually, for modifiers: if mod is En_, and rabbit is enen, it does NOT contain En_.
    /// If rabbit is Enen, it DOES contain En_.
    /// </summary>
    public bool Contains(RabbitGenotype other)
    {
        var thisLoci = Loci.ToDictionary(l => l.GetLocusSymbol());
        foreach (var otherLocus in other.Loci)
        {
            var symbol = otherLocus.GetLocusSymbol();
            if (!thisLoci.TryGetValue(symbol, out var thisLocus)) return false;

            // Check if thisLocus "contains" otherLocus alleles.
            // If otherLocus has 'En', thisLocus must have 'En'.
            bool AllelePresent(Allele target, Locus source) => 
                target.Symbol == "_" || source.First.Symbol == target.Symbol || source.Second.Symbol == target.Symbol;

            if (!AllelePresent(otherLocus.First, thisLocus) || !AllelePresent(otherLocus.Second, thisLocus))
                return false;
        }
        return true;
    }
}
