using System;
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

        if (!f.IsUnknown && !s.IsUnknown && (f.Suspected == null || f.Suspected.Count == 0) && (s.Suspected == null || s.Suspected.Count == 0)) 
            return target;

        if (p1 == null || p2 == null) return target;

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

        var consistent = possibleOffspring.Where(o => target.Matches(o)).ToList();
        consistent = consistent.Where(o => !IsExcluded(o, target)).ToList();

        if (consistent.Count == 0) return target;

        if (consistent.All(o => o.First.Symbol == consistent[0].First.Symbol && o.Second.Symbol == consistent[0].Second.Symbol))
        {
            return consistent[0];
        }

        if (!f.IsUnknown && s.IsUnknown)
        {
            var possibleSeconds = consistent
                .SelectMany(o => new[] { o.First.Symbol, o.Second.Symbol })
                .Where(sym => sym != f.Symbol || consistent.Any(o => o.First.Symbol == f.Symbol && o.Second.Symbol == f.Symbol))
                .Distinct()
                .ToList();

            var actualPossibleSeconds = new List<string>();
            foreach (var sym in possibleSeconds)
            {
                var potentialLocus = SortLocus(new Locus(f, new Allele(sym)) { OverrideLocusSymbol = target.GetLocusSymbol() });
                if (consistent.Any(o => o.First.Symbol == potentialLocus.First.Symbol && o.Second.Symbol == potentialLocus.Second.Symbol))
                {
                    if (sym != "_" || actualPossibleSeconds.Count == 0)
                        actualPossibleSeconds.Add(sym);
                }
            }

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

    public static List<OffspringPrediction> PredictOffspring(RabbitGenotype parent1, RabbitGenotype parent2, int limit = 100, Func<RabbitGenotype, string>? identifyFunc = null)
    {
        var p1Loci = parent1.Loci.ToDictionary(l => l.GetLocusSymbol());
        var p2Loci = parent2.Loci.ToDictionary(l => l.GetLocusSymbol());

        var allSymbols = p1Loci.Keys.Union(p2Loci.Keys).Distinct().ToList();

        // 1. Expand loci based on breed
        void AddBreedLoci(string? breedName, Dictionary<string, Locus> targetLoci)
        {
            if (string.IsNullOrEmpty(breedName)) return;
            var breedDef = VarietyService.Breeds.FirstOrDefault(b => b.Name.Equals(breedName, StringComparison.OrdinalIgnoreCase));
            if (breedDef == null) return;
            var breedGenotype = RabbitGenotype.Parse(breedDef.GenotypeString);
            foreach (var breedLocus in breedGenotype.Loci)
            {
                var symbol = breedLocus.GetLocusSymbol();
                if (!allSymbols.Contains(symbol)) allSymbols.Add(symbol);
                if (!targetLoci.ContainsKey(symbol)) targetLoci[symbol] = breedLocus;
            }
        }

        AddBreedLoci(parent1.PrimaryBreed, p1Loci);
        AddBreedLoci(parent2.PrimaryBreed, p2Loci);

        var locusPossibilities = new Dictionary<string, List<(Locus Locus, double Probability)>>();

        foreach (var symbol in allSymbols)
        {
            p1Loci.TryGetValue(symbol, out var l1);
            p2Loci.TryGetValue(symbol, out var l2);

            var locusDefinition = GeneticParser.Definitions.FirstOrDefault(d => d.Symbol == symbol);
            Locus defaultLocus;
            if (locusDefinition?.DefaultGenotype != null)
            {
                defaultLocus = Locus.Parse(locusDefinition.DefaultGenotype);
                defaultLocus.OverrideLocusSymbol = symbol;
            }
            else
            {
                var defaultAllele = locusDefinition?.DefaultAllele != null ? new Allele(locusDefinition.DefaultAllele.Symbol) : new Allele("_");
                defaultLocus = new Locus(defaultAllele, defaultAllele) { OverrideLocusSymbol = symbol };
            }

            l1 ??= defaultLocus;
            l2 ??= defaultLocus;

            List<Allele> ExpandAllele(Allele a, string locusSymbol)
            {
                var res = new List<Allele>();
                if (a.Symbol == "_")
                {
                    if (a.Suspected is { Count: > 0 })
                    {
                        foreach (var s in a.Suspected)
                        {
                            if (a.Excluded != null && a.Excluded.Contains(s)) continue;
                            res.Add(new Allele(s));
                        }
                    }
                    else
                    {
                        // If no suspected alleles, and we know the locus, expand to all valid alleles
                        var locusDef = GeneticParser.Definitions.FirstOrDefault(d => d.Symbol == locusSymbol);
                        if (locusDef != null)
                        {
                            // If this is the second allele, we only include alleles that are equal or less dominant than the first allele
                            // Actually, Mendelian prediction usually considers all possible genotypes.
                            // But since the user said "_" means "unknown, but equal in dominance or recessive", 
                            // we should probably respect that if we're expanding.
                            // However, we don't know the first allele easily here without more context.
                            // Let's look at the parent locus.
                            p1Loci.TryGetValue(locusSymbol, out var currentL1);
                            p2Loci.TryGetValue(locusSymbol, out var currentL2);
                            
                            Locus? parentLocus = null;
                            if (l1 != null && (l1.First == a || l1.Second == a)) parentLocus = l1;
                            else if (l2 != null && (l2.First == a || l2.Second == a)) parentLocus = l2;
                            else
                            {
                                // If it's not a direct reference, maybe it's a clone? Check by value but we need the specific parent context
                                // Re-fetch from p1Loci/p2Loci
                                if (p1Loci.TryGetValue(locusSymbol, out var p1l) && (p1l.First == a || p1l.Second == a)) parentLocus = p1l;
                                else if (p2Loci.TryGetValue(locusSymbol, out var p2l) && (p2l.First == a || p2l.Second == a)) parentLocus = p2l;
                            }

                            if (parentLocus != null)
                            {
                                var knownAllele = parentLocus.First.IsUnknown ? null : parentLocus.First;
                                if (parentLocus.Second == a && knownAllele != null)
                                {
                                    var knownDef = knownAllele.GetDefinition();
                                    foreach (var alleleDef in locusDef.Alleles)
                                    {
                                        if (a.Excluded != null && a.Excluded.Contains(alleleDef.Symbol)) continue;
                                        if (knownDef != null && alleleDef.Order < knownDef.Order) continue;
                                        res.Add(new Allele(alleleDef.Symbol));
                                    }
                                }
                                else
                                {
                                    // First allele is unknown or we don't have a known allele to bound it
                                    foreach (var alleleDef in locusDef.Alleles)
                                    {
                                        if (a.Excluded != null && a.Excluded.Contains(alleleDef.Symbol)) continue;
                                        res.Add(new Allele(alleleDef.Symbol));
                                    }
                                }
                            }
                            else
                            {
                                res.Add(new Allele("_"));
                            }
                        }
                        else
                        {
                            res.Add(new Allele("_"));
                        }
                    }
                }
                else
                {
                    res.Add(a);
                }
                return res;
            }

            var p1Gametes = new List<Allele>();
            p1Gametes.AddRange(ExpandAllele(l1.First, symbol));
            p1Gametes.AddRange(ExpandAllele(l1.Second, symbol));

            var p2Gametes = new List<Allele>();
            p2Gametes.AddRange(ExpandAllele(l2.First, symbol));
            p2Gametes.AddRange(ExpandAllele(l2.Second, symbol));

                    var outcomes = new Dictionary<string, (Locus Locus, double Probability)>();
                    double totalCombos = (double)p1Gametes.Count * p2Gametes.Count;

                    foreach (var g1 in p1Gametes)
                    {
                        foreach (var g2 in p2Gametes)
                        {
                            var offspringLocus = new Locus(g1, g2) { OverrideLocusSymbol = symbol }.Normalize();

                            // Normalization and simplification for homozygous dominant
                            if (offspringLocus.First.Symbol == "_" && offspringLocus.Second.Symbol == "_")
                            {
                                offspringLocus = new Locus(new Allele("_"), new Allele("_")) { OverrideLocusSymbol = symbol };
                            }
                            else if (offspringLocus.First.Symbol == offspringLocus.Second.Symbol)
                            {
                                var def = offspringLocus.First.GetDefinition();
                                if (def != null && def.Dominance == DominanceType.Dominant)
                                {
                                    if (symbol != "Dw")
                                    {
                                        offspringLocus = new Locus(offspringLocus.First, new Allele("_")) { OverrideLocusSymbol = symbol };
                                    }
                                }
                            }
                            
                            var key = offspringLocus.ToString();
                            if (outcomes.TryGetValue(key, out var existing))
                            {
                                outcomes[key] = (existing.Locus, existing.Probability + (1.0 / totalCombos));
                            }
                            else
                            {
                                outcomes[key] = (offspringLocus, 1.0 / totalCombos);
                            }
                        }
                    }

                    locusPossibilities[symbol] = outcomes.Values.ToList();
                }

                var currentPredictions = new List<OffspringPrediction> { new OffspringPrediction(new RabbitGenotype(), 1.0) };
                foreach (var symbol in allSymbols)
                {
                    var nextPredictions = new List<OffspringPrediction>();
                    var possibilities = locusPossibilities[symbol];
                    foreach (var current in currentPredictions)
                    {
                        foreach (var possibility in possibilities)
                        {
                            var newGenotype = new RabbitGenotype();
                            newGenotype.Loci.AddRange(current.Genotype.Loci);
                            newGenotype.Loci.Add(possibility.Locus);
                            nextPredictions.Add(new OffspringPrediction(newGenotype, current.Probability * possibility.Probability));
                        }
                    }
                    currentPredictions = nextPredictions;
                }

                // Determine breed outcome
                string? resultBreed = null;
                if (parent1.PrimaryBreed == parent2.PrimaryBreed) resultBreed = parent1.PrimaryBreed;
                else if (parent1.PrimaryBreed != null && parent2.PrimaryBreed != null) resultBreed = "Mixed Breed";
                else resultBreed = parent1.PrimaryBreed ?? parent2.PrimaryBreed;

                foreach (var p in currentPredictions) p.Genotype.PrimaryBreed = resultBreed;

                // Consolidate for final presentation.
                // NOTE: We MUST separate genotypes that have different phenotypes for body/fur/pattern categories.
                // But the variety identification might merge them.
                return currentPredictions
                    .GroupBy(p => {
                        var variety = identifyFunc != null ? identifyFunc(p.Genotype) : p.Genotype.ToString();
                        // Also include Dw, L, M, R, Sa, A, En, V, We loci state in the key to ensure they don't merge if they differ
                        var specialLoci = p.Genotype.Loci.Where(l => {
                            var sym = l.GetLocusSymbol();
                            return sym == "Dw" || sym == "L" || sym == "M" || sym == "R" || sym == "Sa" || sym == "A" || sym == "En" || sym == "V" || sym == "We";
                        }).Select(l => l.ToString());
                        return variety + "|" + string.Join(",", specialLoci);
                    })
                    .Select(g => {
                        var best = g.OrderByDescending(x => CountKnownAlleles(x.Genotype))
                                    .ThenBy(x => x.Genotype.ToString().Length)
                                    .First();
                        return new OffspringPrediction(best.Genotype, g.Sum(p => p.Probability));
                    })
                    .OrderByDescending(p => p.Probability)
                    .ToList();
            }

    private static int CountKnownAlleles(RabbitGenotype g)
    {
        return g.Loci.Sum(l => (l.First.IsUnknown ? 0 : 1) + (l.Second.IsUnknown ? 0 : 1));
    }

    public record OffspringPrediction(RabbitGenotype Genotype, double Probability)
    {
        public string? Description { get; set; }
    }

    private static List<Allele> GetExpandedGametes(Locus locus)
    {
        var locusSymbol = locus.GetLocusSymbol();
        var locusDef = GeneticParser.Definitions.FirstOrDefault(d => d.Symbol == locusSymbol);
        var allPossibleAlleles = locusDef?.Alleles.Select(a => a.Symbol).ToList() ?? new List<string>();

        List<Allele> Expand(Allele allele)
        {
            var options = new List<Allele>();
            if (allele.IsUnknown)
            {
                var candidates = new List<string>();
                if (allele.Suspected is { Count: > 0 }) candidates.AddRange(allele.Suspected);
                else {
                    candidates.AddRange(allPossibleAlleles);
                    if (candidates.Count == 0) candidates.Add("_");
                }
                foreach (var s in candidates) {
                    if (allele.Excluded != null && allele.Excluded.Contains(s)) continue;
                    options.Add(new Allele(s));
                }
                if (options.Count == 0) options.Add(new Allele("_"));
            }
            else options.Add(allele);
            return options;
        }

        var firstOptions = Expand(locus.First);
        var secondOptions = Expand(locus.Second);
        var result = new List<Allele>();
        foreach (var f in firstOptions) result.Add(f);
        foreach (var s in secondOptions) result.Add(s);
        return result;
    }

    private static bool IsExcluded(Locus offspring, Locus target)
    {
        var excluded = new List<string>();
        if (target.First.Excluded != null) excluded.AddRange(target.First.Excluded);
        if (target.Second.Excluded != null) excluded.AddRange(target.Second.Excluded);
        if (excluded.Count == 0) return false;
        return excluded.Contains(offspring.First.Symbol) || excluded.Contains(offspring.Second.Symbol);
    }

    public static (RabbitGenotype SolvedParent1, RabbitGenotype SolvedParent2) SolveParents(
        RabbitGenotype parent1, RabbitGenotype parent2, IEnumerable<RabbitGenotype> offspring)
    {
        var solvedP1 = new RabbitGenotype();
        var solvedP2 = new RabbitGenotype();
        var p1Loci = parent1.Loci.ToDictionary(l => l.GetLocusSymbol());
        var p2Loci = parent2.Loci.ToDictionary(l => l.GetLocusSymbol());
        var allSymbols = p1Loci.Keys.Union(p2Loci.Keys).Distinct().ToList();
        foreach (var child in offspring)
            foreach (var l in child.Loci)
                if (!allSymbols.Contains(l.GetLocusSymbol())) allSymbols.Add(l.GetLocusSymbol());

        foreach (var symbol in allSymbols)
        {
            p1Loci.TryGetValue(symbol, out var p1Locus);
            p2Loci.TryGetValue(symbol, out var p2Locus);
            p1Locus ??= new Locus(new Allele("_"), new Allele("_")) { OverrideLocusSymbol = symbol };
            p2Locus ??= new Locus(new Allele("_"), new Allele("_")) { OverrideLocusSymbol = symbol };
            var childrenLoci = offspring.Select(c => c.Loci.FirstOrDefault(l => l.GetLocusSymbol() == symbol)).Where(l => l != null).ToList();
            var (newP1, newP2) = SolveParentLocus(p1Locus, p2Locus, childrenLoci!);
            solvedP1.Loci.Add(newP1);
            solvedP2.Loci.Add(newP2);
        }
        return (solvedP1, solvedP2);
    }

    private static (Locus NewP1, Locus NewP2) SolveParentLocus(Locus p1, Locus p2, List<Locus> children)
    {
        if (children.Count == 0) return (p1, p2);
        var knownAlleles = children.SelectMany(c => new[] { c.First, c.Second }).Union(new[] { p1.First, p1.Second, p2.First, p2.Second }).Where(a => !a.IsUnknown).Select(a => a.Symbol).Distinct().ToList();

        List<Allele> ExpandParent(Locus p) {
            var options = new HashSet<string>();
            if (!p.First.IsUnknown) options.Add(p.First.Symbol);
            if (!p.Second.IsUnknown) options.Add(p.Second.Symbol);
            if (p.First.Suspected != null) foreach (var s in p.First.Suspected) if (p.First.Excluded == null || !p.First.Excluded.Contains(s)) options.Add(s);
            if (p.Second.Suspected != null) foreach (var s in p.Second.Suspected) if (p.Second.Excluded == null || !p.Second.Excluded.Contains(s)) options.Add(s);
            if (p.First.IsUnknown || p.Second.IsUnknown) {
                var pExcluded = new HashSet<string>();
                if (p.First.Excluded != null) foreach (var e in p.First.Excluded) pExcluded.Add(e);
                if (p.Second.Excluded != null) foreach (var e in p.Second.Excluded) pExcluded.Add(e);
                foreach (var ka in knownAlleles) if (!pExcluded.Contains(ka)) options.Add(ka);
            }
            return options.Select(s => new Allele(s)).ToList();
        }

        var p1Options = ExpandParent(p1);
        var p2Options = ExpandParent(p2);
        var validParentPairs = new List<(Locus P1, Locus P2)>();

        foreach (var a1 in p1Options) {
            foreach (var b1 in p1Options) {
                var potP1 = SortLocus(new Locus(a1, b1) { OverrideLocusSymbol = p1.GetLocusSymbol() });
                if (!p1.Matches(potP1) || IsExcluded(potP1, p1)) continue;
                foreach (var a2 in p2Options) {
                    foreach (var b2 in p2Options) {
                        var potP2 = SortLocus(new Locus(a2, b2) { OverrideLocusSymbol = p2.GetLocusSymbol() });
                        if (!p2.Matches(potP2) || IsExcluded(potP2, p2)) continue;
                        if (CanProduceAll(potP1, potP2, children)) {
                            if (!validParentPairs.Any(pair => pair.P1.First.Symbol == potP1.First.Symbol && pair.P1.Second.Symbol == potP1.Second.Symbol && pair.P2.First.Symbol == potP2.First.Symbol && pair.P2.Second.Symbol == potP2.Second.Symbol))
                                validParentPairs.Add((potP1, potP2));
                        }
                    }
                }
            }
        }
        if (validParentPairs.Count == 0) return (p1, p2);
        return (ResolveCommonality(p1, validParentPairs.Select(pair => pair.P1).ToList()), ResolveCommonality(p2, validParentPairs.Select(pair => pair.P2).ToList()));
    }

    private static bool CanProduceAll(Locus p1, Locus p2, List<Locus> children)
    {
        var possible = new List<(string, string)>();
        foreach (var g1 in new[] { p1.First.Symbol, p1.Second.Symbol })
            foreach (var g2 in new[] { p2.First.Symbol, p2.Second.Symbol })
                if (g1 != "_" && g2 != "_") possible.Add(g1.CompareTo(g2) <= 0 ? (g1, g2) : (g2, g1));

        foreach (var child in children) {
            if (!child.First.IsUnknown && !child.Second.IsUnknown) {
                var cs = child.First.Symbol.CompareTo(child.Second.Symbol) <= 0 ? (child.First.Symbol, child.Second.Symbol) : (child.Second.Symbol, child.First.Symbol);
                if (!possible.Contains(cs)) return false;
            } else {
                bool foundMatch = false;
                foreach (var pos in possible) {
                    var posLocus = new Locus(new Allele(pos.Item1), new Allele(pos.Item2)) { OverrideLocusSymbol = child.GetLocusSymbol() };
                    if (child.Matches(posLocus) && !IsExcluded(posLocus, child)) { foundMatch = true; break; }
                }
                if (!foundMatch) return false;
            }
        }
        return true;
    }

    private static Locus ResolveCommonality(Locus original, List<Locus> possibilities)
    {
        if (possibilities.Count == 0) return original;
        if (possibilities.All(p => p.First.Symbol == possibilities[0].First.Symbol && p.Second.Symbol == possibilities[0].Second.Symbol)) return possibilities[0];
        var newFirst = original.First;
        var newSecond = original.Second;
        var firsts = possibilities.Select(p => p.First.Symbol).Distinct().ToList();
        var seconds = possibilities.Select(p => p.Second.Symbol).Distinct().ToList();
        if (firsts.Count == 1) newFirst = new Allele(firsts[0]);
        else {
            var clean = firsts.Where(s => s != "_").ToList();
            if (clean.Count == 1 && firsts.Contains("_")) newFirst = new Allele(clean[0]); 
            else if (clean.Count > 1) newFirst = new Allele("_", Suspected: clean.OrderBy(s => s).ToList(), UseSlashInSuspected: true);
        }
        if (seconds.Count == 1) newSecond = new Allele(seconds[0]);
        else {
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
        if (!f.IsUnknown && !s.IsUnknown && f.Symbol != s.Symbol) {
            var def1 = def.Alleles.FirstOrDefault(a => a.Symbol == f.Symbol);
            var def2 = def.Alleles.FirstOrDefault(a => a.Symbol == s.Symbol);
            if (def1 != null && def2 != null && def2.Order < def1.Order) return new Locus(s, f) { OverrideLocusSymbol = symbol };
        }
        if (f.IsUnknown && !s.IsUnknown) return new Locus(s, f) { OverrideLocusSymbol = symbol };
        return locus;
    }
}
