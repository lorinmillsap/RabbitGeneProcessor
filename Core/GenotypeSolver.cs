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

    /// <summary>
    /// Resolves unknown alleles ('_') in two target parents by analyzing the genotypes of their offspring.
    /// This generates new genotype layers for the parents without modifying the original inputs.
    /// </summary>
    public static (RabbitGenotype SolvedParent1, RabbitGenotype SolvedParent2) SolveParents(
        RabbitGenotype parent1, 
        RabbitGenotype parent2, 
        IEnumerable<RabbitGenotype> offspring)
    {
        var solvedP1 = new RabbitGenotype();
        var solvedP2 = new RabbitGenotype();
        
        var p1Loci = parent1.Loci.ToDictionary(l => l.GetLocusSymbol());
        var p2Loci = parent2.Loci.ToDictionary(l => l.GetLocusSymbol());
        
        // Collect all symbols involved
        var allSymbols = p1Loci.Keys.Union(p2Loci.Keys).Distinct().ToList();
        foreach (var child in offspring)
        {
            foreach (var l in child.Loci)
            {
                var sym = l.GetLocusSymbol();
                if (!allSymbols.Contains(sym)) allSymbols.Add(sym);
            }
        }

        foreach (var symbol in allSymbols)
        {
            p1Loci.TryGetValue(symbol, out var p1Locus);
            p2Loci.TryGetValue(symbol, out var p2Locus);
            
            p1Locus ??= new Locus(new Allele("_"), new Allele("_")) { OverrideLocusSymbol = symbol };
            p2Locus ??= new Locus(new Allele("_"), new Allele("_")) { OverrideLocusSymbol = symbol };

            var childrenLoci = offspring
                .Select(c => c.Loci.FirstOrDefault(l => l.GetLocusSymbol() == symbol))
                .Where(l => l != null)
                .ToList();

            var (newP1, newP2) = SolveParentLocus(p1Locus, p2Locus, childrenLoci!);
            solvedP1.Loci.Add(newP1);
            solvedP2.Loci.Add(newP2);
        }

        return (solvedP1, solvedP2);
    }

    private static (Locus NewP1, Locus NewP2) SolveParentLocus(Locus p1, Locus p2, List<Locus> children)
    {
        // If no children, can't solve anything new
        if (children.Count == 0) return (p1, p2);

        // Generate all possible expanded allele combinations for both parents
        var p1AlleleOptions = GetExpandedGametes(p1);
        var p2AlleleOptions = GetExpandedGametes(p2);

        // We are looking for pairs of parental loci (P1, P2) that are consistent with ALL children.
        // A parental locus pair is consistent if for every child, it's possible for that child
        // to have been produced by those parents.
        
        // First, let's identify what possible alleles exist in the population for this locus
        // to fill in any truly unknown underscores if needed.
        var knownAlleles = children
            .SelectMany(c => new[] { c.First, c.Second })
            .Union(new[] { p1.First, p1.Second, p2.First, p2.Second })
            .Where(a => !a.IsUnknown)
            .Select(a => a.Symbol)
            .Distinct()
            .ToList();

        // If a parent has _, it could be any of the known alleles in the population 
        // OR it could be something else (represented by _).
        List<Allele> ExpandParent(Locus p)
        {
            var options = new HashSet<string>();
            if (!p.First.IsUnknown) options.Add(p.First.Symbol);
            if (!p.Second.IsUnknown) options.Add(p.Second.Symbol);
            
            // Add suspects
            if (p.First.Suspected != null) foreach (var s in p.First.Suspected) options.Add(s);
            if (p.Second.Suspected != null) foreach (var s in p.Second.Suspected) options.Add(s);
            
            // If there's an underscore, it could be any allele seen in children or the other parent
            if (p.First.IsUnknown || p.Second.IsUnknown)
            {
                foreach (var ka in knownAlleles) options.Add(ka);
                options.Add("_");
            }
            
            return options.Select(s => new Allele(s)).ToList();
        }

        var p1Options = ExpandParent(p1);
        var p2Options = ExpandParent(p2);

        var validParentPairs = new List<(Locus P1, Locus P2)>();

        foreach (var a1 in p1Options)
        {
            foreach (var b1 in p1Options)
            {
                var potP1 = SortLocus(new Locus(a1, b1) { OverrideLocusSymbol = p1.GetLocusSymbol() });
                // Check if potP1 is consistent with p1 (matches and doesn't have excluded)
                if (!p1.Matches(potP1) || IsExcluded(potP1, p1)) continue;

                foreach (var a2 in p2Options)
                {
                    foreach (var b2 in p2Options)
                    {
                        var potP2 = SortLocus(new Locus(a2, b2) { OverrideLocusSymbol = p2.GetLocusSymbol() });
                        if (!p2.Matches(potP2) || IsExcluded(potP2, p2)) continue;

                        // Now check if this PAIR of parents can produce ALL children
                        if (CanProduceAll(potP1, potP2, children))
                        {
                            if (!validParentPairs.Any(pair => 
                                pair.P1.First.Symbol == potP1.First.Symbol && pair.P1.Second.Symbol == potP1.Second.Symbol &&
                                pair.P2.First.Symbol == potP2.First.Symbol && pair.P2.Second.Symbol == potP2.Second.Symbol))
                            {
                                validParentPairs.Add((potP1, potP2));
                            }
                        }
                    }
                }
            }
        }

        if (validParentPairs.Count == 0) return (p1, p2);

        // Analyze validParentPairs to see what we can deduce
        var resP1 = ResolveCommonality(p1, validParentPairs.Select(pair => pair.P1).ToList());
        var resP2 = ResolveCommonality(p2, validParentPairs.Select(pair => pair.P2).ToList());

        return (resP1, resP2);
    }

    private static bool CanProduceAll(Locus p1, Locus p2, List<Locus> children)
    {
        var p1G = new[] { p1.First.Symbol, p1.Second.Symbol };
        var p2G = new[] { p2.First.Symbol, p2.Second.Symbol };

        // Possible child genotypes from these specific parents
        var possible = new List<(string, string)>();
        foreach (var g1 in p1G)
        {
            foreach (var g2 in p2G)
            {
                if (g1 == "_" || g2 == "_") continue; // If parent has _, we can't be sure, but usually we expanded parents to concrete alleles
                possible.Add(g1.CompareTo(g2) <= 0 ? (g1, g2) : (g2, g1));
            }
        }

        foreach (var child in children)
        {
            // If child is fully known, it must be in 'possible'
            if (!child.First.IsUnknown && !child.Second.IsUnknown)
            {
                var cs = child.First.Symbol.CompareTo(child.Second.Symbol) <= 0 
                    ? (child.First.Symbol, child.Second.Symbol) 
                    : (child.Second.Symbol, child.First.Symbol);
                
                if (!possible.Contains(cs)) return false;
            }
            else
            {
                // Child is partially known, at least one allele combination from parents must match child
                bool foundMatch = false;
                foreach (var pos in possible)
                {
                    var posLocus = new Locus(new Allele(pos.Item1), new Allele(pos.Item2)) { OverrideLocusSymbol = child.GetLocusSymbol() };
                    if (child.Matches(posLocus) && !IsExcluded(posLocus, child))
                    {
                        foundMatch = true;
                        break;
                    }
                }
                if (!foundMatch) return false;
            }
        }
        return true;
    }

    private static Locus ResolveCommonality(Locus original, List<Locus> possibilities)
    {
        if (possibilities.Count == 0) return original;
        
        // If all possibilities are the same
        if (possibilities.All(p => p.First.Symbol == possibilities[0].First.Symbol && p.Second.Symbol == possibilities[0].Second.Symbol))
        {
            return possibilities[0];
        }

        var newFirst = original.First;
        var newSecond = original.Second;

        // Collect all symbols that appear in each position
        var firsts = possibilities.Select(p => p.First.Symbol).Distinct().ToList();
        var seconds = possibilities.Select(p => p.Second.Symbol).Distinct().ToList();
        
        // A simple but effective rule: 
        // If an allele symbol appears in EVERY possible valid genotype for this parent, it's proven.
        // For first position:
        if (firsts.Count == 1)
        {
            newFirst = new Allele(firsts[0]);
        }
        else
        {
            var clean = firsts.Where(s => s != "_").ToList();
            if (clean.Count == 1 && firsts.Contains("_")) newFirst = new Allele(clean[0]); 
            else if (clean.Count > 1) newFirst = new Allele("_", Suspected: clean.OrderBy(s => s).ToList(), UseSlashInSuspected: true);
        }

        // For second position:
        if (seconds.Count == 1)
        {
            newSecond = new Allele(seconds[0]);
        }
        else
        {
            var clean = seconds.Where(s => s != "_").ToList();
            if (clean.Count == 1 && seconds.Contains("_")) newSecond = new Allele(clean[0]); 
            else if (clean.Count > 1) newSecond = new Allele("_", Suspected: clean.OrderBy(s => s).ToList(), UseSlashInSuspected: true);
        }

        return SortLocus(new Locus(newFirst, newSecond) { OverrideLocusSymbol = original.GetLocusSymbol() });
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
