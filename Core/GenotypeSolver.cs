using System.Collections.Generic;
using System.Linq;

namespace RabbitGeneProcessor.Core;

/// <summary>
/// Provides logic for resolving unknown genetic alleles based on parental inheritance.
/// </summary>
public static class GenotypeSolver
{
    /// <summary>
    /// Resolves unknown alleles ('_') in a target genotype by analyzing the genotypes of its parents.
    /// This generates a new genotype layer without modifying the original inputs.
    /// </summary>
    public static RabbitGenotype Solve(RabbitGenotype target, RabbitGenotype parent1, RabbitGenotype parent2)
    {
        var solvedGenotype = new RabbitGenotype();
        var targetLoci = target.Loci.ToDictionary(l => l.GetLocusSymbol());
        var p1Loci = parent1.Loci.ToDictionary(l => l.GetLocusSymbol());
        var p2Loci = parent2.Loci.ToDictionary(l => l.GetLocusSymbol());

        // We want to iterate through all known loci from the definitions or the union of all involved genotypes
        var allSymbols = targetLoci.Keys
            .Union(p1Loci.Keys)
            .Union(p2Loci.Keys)
            .Distinct();

        foreach (var symbol in allSymbols)
        {
            targetLoci.TryGetValue(symbol, out var tLocus);
            p1Loci.TryGetValue(symbol, out var p1Locus);
            p2Loci.TryGetValue(symbol, out var p2Locus);

            if (tLocus == null)
            {
                // If the target doesn't have this locus yet, we might still be able to deduce it from parents
                // but usually the solver is called on a "CalculatedGenotype" that already has baseline loci.
                // If it's missing, let's skip or initialize as __
                tLocus = new Locus(new Allele("_"), new Allele("_")) { OverrideLocusSymbol = symbol };
            }

            var solvedLocus = SolveLocus(tLocus, p1Locus, p2Locus);
            solvedGenotype.Loci.Add(solvedLocus);
        }

        return solvedGenotype;
    }

    private static Locus SolveLocus(Locus target, Locus? p1, Locus? p2)
    {
        var f = target.First;
        var s = target.Second;

        // If target is already fully known, return it
        if (!f.IsUnknown && !s.IsUnknown) return target;

        // Parent resolution logic:
        // If a parent is homozygous (e.g., aa), the offspring MUST have inherited 'a'.
        
        void ApplyParentalRule(Locus? parent, ref Allele first, ref Allele second)
        {
            if (parent == null) return;
            
            // homozygous rule
            if (!parent.First.IsUnknown && parent.First.Symbol == parent.Second.Symbol)
            {
                var inherited = parent.First;
                
                // If target doesn't have this allele yet, fill one underscore
                if (first.Symbol != inherited.Symbol && second.Symbol != inherited.Symbol)
                {
                    if (first.IsUnknown) first = inherited;
                    else if (second.IsUnknown) second = inherited;
                }
            }
        }

        ApplyParentalRule(p1, ref f, ref s);
        ApplyParentalRule(p2, ref f, ref s);

        // Sorting by dominance to keep it consistent
        var result = new Locus(f, s) { OverrideLocusSymbol = target.GetLocusSymbol() };
        return SortLocus(result);
    }

    private static Locus SortLocus(Locus locus)
    {
        var symbol = locus.GetLocusSymbol();
        if (symbol == "Unknown") return locus;

        var def = GeneticParser.Definitions.FirstOrDefault(d => d.Symbol == symbol);
        if (def == null) return locus;

        var f = locus.First;
        var s = locus.Second;

        if (!f.IsUnknown && !s.IsUnknown && f.Symbol != s.Symbol)
        {
            var def1 = def.Alleles.FirstOrDefault(a => a.Symbol == f.Symbol);
            var def2 = def.Alleles.FirstOrDefault(a => a.Symbol == s.Symbol);

            if (def1 != null && def2 != null && def2.Order < def1.Order)
            {
                return new Locus(s, f) { OverrideLocusSymbol = symbol };
            }
        }
        
        // If one is unknown, make sure unknown is in the second position
        if (!f.IsUnknown && s.IsUnknown)
        {
             // Already correct
        }
        else if (f.IsUnknown && !s.IsUnknown)
        {
            return new Locus(s, f) { OverrideLocusSymbol = symbol };
        }

        return locus;
    }
}
