namespace RabbitGeneProcessor.Core;

/// <summary>
/// Represents the full genotype of a rabbit.
/// </summary>
public class RabbitGenotype
{
    public List<Locus> Loci { get; } = new();

    /// <summary>
    /// Parses a genotype string (e.g., "A_,B_,C_,D_,E,enen", "{A}__").
    /// </summary>
    public static RabbitGenotype Parse(string input)
    {
        var genotype = new RabbitGenotype();
        var parts = input.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var part in parts)
        {
            if (part.StartsWith('{'))
            {
                var endBraceIndex = part.IndexOf('}');
                if (endBraceIndex > 1)
                {
                    var locusSymbol = part.Substring(1, endBraceIndex - 1);
                    var allelesString = part.Substring(endBraceIndex + 1);
                    var locus = Locus.Parse(allelesString);
                    locus.OverrideLocusSymbol = locusSymbol;
                    // If the input was something like "__", and we forced an override to A,
                    // we need to make sure the alleles are just unknown alleles, not tied to a locus yet.
                    if (allelesString == "__" || allelesString == "_")
                    {
                         locus = new Locus(new Allele("_"), new Allele("_")) { OverrideLocusSymbol = locusSymbol };
                    }
                    genotype.Loci.Add(locus);
                    continue;
                }
            }
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
        var thisLoci = Loci.GroupBy(l => l.GetLocusSymbol()).ToDictionary(g => g.Key, g => g.First());
        var otherLoci = other.Loci.GroupBy(l => l.GetLocusSymbol()).ToDictionary(g => g.Key, g => g.First());

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
    /// This is stricter than Match: the other's alleles must be present in this genotype.
    /// Handles dominant gene pairing: A_ contains Aa.
    /// </summary>
    public bool Contains(RabbitGenotype other)
    {
        var thisLoci = Loci.GroupBy(l => l.GetLocusSymbol()).ToDictionary(g => g.Key, g => g.First());
        foreach (var otherLocus in other.Loci)
        {
            var symbol = otherLocus.GetLocusSymbol();
            if (symbol == "Unknown") continue;
            if (!thisLoci.TryGetValue(symbol, out var thisLocus)) return false;

            // Normalize both for consistent comparison
            var normThis = thisLocus.Normalize();
            var normOther = otherLocus.Normalize();

            // Check if normThis "contains" normOther alleles.
            // Rule: Dominant genes are always dominant.
            // A_ should contain Aa because A is dominant and matches, and _ can be anything.
            
            bool AlleleMatches(Allele source, Allele target)
            {
                if (source.Symbol == "_" ) return true;
                // If the target has an underscore, it means it's a phenotype mask (e.g. A_)
                // In that case, the source must have the dominant allele.
                if (target.Symbol == "_") return true; 
                return source.Symbol == target.Symbol;
            }

            // After normalization, First is the most dominant.
            // Special rule: if normOther.Second is NOT _, it means the variety definition 
            // EXPLICITLY requires that allele (e.g. Siamese Sable cchlcchl).
            // In that case, normThis.Second MUST NOT be _ if we want to match it.
            // Wait, if normThis is cchlcchl and normOther is cchl_, it matches (Contains).
            // If normThis is cchl_ and normOther is cchlcchl, it should NOT match.
            if (normOther.Second.Symbol != "_" && normThis.Second.Symbol == "_") return false;

            if (!AlleleMatches(normThis.First, normOther.First)) return false;
            if (!AlleleMatches(normThis.Second, normOther.Second)) return false;
        }
        return true;
    }
}
