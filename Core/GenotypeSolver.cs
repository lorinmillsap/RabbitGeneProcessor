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
        if (!f.IsUnknown && !s.IsUnknown && (f.Suspected == null || f.Suspected.Count == 0) && (s.Suspected == null || s.Suspected.Count == 0)) 
            return target;

        if (p1 == null || p2 == null) return target;

        // Punnett Square Analysis
        var p1Gametes = GetExpandedGametes(p1);
        var p2Gametes = GetExpandedGametes(p2);

        var possibleOffspring = new List<Locus>();
        foreach (var g1 in p1Gametes)
        {
            foreach (var g2 in p2Gametes)
            {
                possibleOffspring.Add(SortLocus(new Locus(g1, g2) { OverrideLocusSymbol = target.GetLocusSymbol() }));
            }
        }

        // Filter possible offspring by what matches the target
        var consistent = possibleOffspring.Where(o => target.Matches(o)).ToList();
        
        // Further filter by target's exclusions
        consistent = consistent.Where(o => !IsExcluded(o, target)).ToList();

        if (consistent.Count == 0) return target;

        // If all consistent offspring have the same genotype, we solved it
        if (consistent.All(o => o.First.Symbol == consistent[0].First.Symbol && o.Second.Symbol == consistent[0].Second.Symbol))
        {
            return consistent[0];
        }

        // Otherwise, we have multiple possibilities. Try to find commonalities or suspects.
        // We assume target is like A_ or aa.
        // If target is A_, we are trying to solve the second slot.
        if (!f.IsUnknown && s.IsUnknown)
        {
            var possibleSeconds = consistent
                .SelectMany(o => new[] { o.First.Symbol, o.Second.Symbol })
                .Where(sym => sym != f.Symbol || consistent.Any(o => o.First.Symbol == f.Symbol && o.Second.Symbol == f.Symbol)) // Keep f if homozygous is possible
                .Distinct()
                .ToList();

            // Refined logic: the possible second alleles are those that, when paired with 'f', match 'target'
            // and are present in the consistent list.
            var actualPossibleSeconds = new List<string>();
            foreach (var sym in possibleSeconds)
            {
                var potentialLocus = SortLocus(new Locus(f, new Allele(sym)) { OverrideLocusSymbol = target.GetLocusSymbol() });
                if (consistent.Any(o => o.First.Symbol == potentialLocus.First.Symbol && o.Second.Symbol == potentialLocus.Second.Symbol))
                {
                    if (sym != "_" || actualPossibleSeconds.Count == 0) // Avoid adding _ if we have better options
                        actualPossibleSeconds.Add(sym);
                }
            }

            // If we have concrete options and an underscore, remove the underscore if there are multiple concrete ones?
            // Actually, if we have A and a, then _ is redundant.
            if (actualPossibleSeconds.Count > 1 && actualPossibleSeconds.Contains("_"))
            {
                actualPossibleSeconds.Remove("_");
            }
            
            if (actualPossibleSeconds.Count == 1)
            {
                return SortLocus(new Locus(f, new Allele(actualPossibleSeconds[0])) { OverrideLocusSymbol = target.GetLocusSymbol() });
            }
            
            if (actualPossibleSeconds.Count > 1)
            {
                var newS = new Allele("_", Suspected: actualPossibleSeconds.OrderBy(x => x).ToList(), UseSlashInSuspected: true);
                return new Locus(f, newS) { OverrideLocusSymbol = target.GetLocusSymbol() };
            }
        }

        return target;
    }

    private static List<Allele> GetExpandedGametes(Locus locus)
    {
        var gametes = new List<Allele> { locus.First, locus.Second };
        var expanded = new List<Allele>();
        foreach (var g in gametes)
        {
            if (g.IsUnknown && g.Suspected is { Count: > 0 })
            {
                foreach (var s in g.Suspected)
                {
                    expanded.Add(new Allele(s));
                }
            }
            else
            {
                expanded.Add(g);
            }
        }
        return expanded;
    }

    private static bool IsExcluded(Locus offspring, Locus target)
    {
        var excluded = new List<string>();
        if (target.First.Excluded != null) excluded.AddRange(target.First.Excluded);
        if (target.Second.Excluded != null) excluded.AddRange(target.Second.Excluded);
        
        if (excluded.Count == 0) return false;
        
        return excluded.Contains(offspring.First.Symbol) || excluded.Contains(offspring.Second.Symbol);
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
