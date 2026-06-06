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

    /// <summary>
    /// Predicts all possible offspring genotypes from a pairing of two genotypes.
    /// </summary>
    /// <param name="parent1">The first parent's genotype.</param>
    /// <param name="parent2">The second parent's genotype.</param>
    /// <param name="limit">The maximum number of outcomes to return.</param>
    /// <returns>A list of predicted offspring genotypes with their probabilities.</returns>
    public static List<OffspringPrediction> PredictOffspring(RabbitGenotype parent1, RabbitGenotype parent2, int limit = 100, Func<RabbitGenotype, string>? identifyFunc = null)
    {
        var p1Loci = parent1.Loci.ToDictionary(l => l.GetLocusSymbol());
        var p2Loci = parent2.Loci.ToDictionary(l => l.GetLocusSymbol());

        var allSymbols = p1Loci.Keys.Union(p2Loci.Keys).Distinct().ToList();

        // If a breed is specified in either parent, we should bring in its default loci if they are missing
        if (!string.IsNullOrEmpty(parent1.PrimaryBreed))
        {
            var breedDef = VarietyService.Breeds.FirstOrDefault(b => b.Name.Equals(parent1.PrimaryBreed, StringComparison.OrdinalIgnoreCase));
            if (breedDef != null)
            {
                var breedGenotype = RabbitGenotype.Parse(breedDef.GenotypeString);
                foreach (var symbol in breedGenotype.Loci.Select(l => l.GetLocusSymbol()))
                {
                    if (!allSymbols.Contains(symbol)) allSymbols.Add(symbol);
                }
            }
        }
        if (!string.IsNullOrEmpty(parent2.PrimaryBreed))
        {
            var breedDef = VarietyService.Breeds.FirstOrDefault(b => b.Name.Equals(parent2.PrimaryBreed, StringComparison.OrdinalIgnoreCase));
            if (breedDef != null)
            {
                var breedGenotype = RabbitGenotype.Parse(breedDef.GenotypeString);
                foreach (var symbol in breedGenotype.Loci.Select(l => l.GetLocusSymbol()))
                {
                    if (!allSymbols.Contains(symbol)) allSymbols.Add(symbol);
                }
            }
        }
        
        // Per-locus possibilities: Symbol -> List of (Locus, Probability)
        var locusPossibilities = new Dictionary<string, List<(Locus Locus, double Probability)>>();

        foreach (var symbol in allSymbols)
        {
            p1Loci.TryGetValue(symbol, out var l1);
            p2Loci.TryGetValue(symbol, out var l2);

            // If missing, use default allele from locus definition if available, otherwise _
            var locusDefinition = GeneticParser.Definitions.FirstOrDefault(d => d.Symbol == symbol);
            var defaultAllele = locusDefinition?.DefaultAllele != null ? new Allele(locusDefinition.DefaultAllele.Symbol) : new Allele("_");

            l1 ??= new Locus(defaultAllele, defaultAllele) { OverrideLocusSymbol = symbol };
            l2 ??= new Locus(defaultAllele, defaultAllele) { OverrideLocusSymbol = symbol };

            // Logic for expanding alleles:
            // 1. If an allele is known (not _), it is used as is.
            // 2. If an allele is unknown (_), but has suspected alleles (), we use those suspected alleles.
            // 3. If an allele is unknown (_), and has NO suspected alleles, we keep it as _ to avoid explosion.
            //    EXCEPTION: For stacking loci, we ALWAYS expand unknown alleles to explore hidden recessives.
            // 4. Exclusions [] are always respected.
            
            bool hasKnownRecessive = (l1.First.Symbol != "_" && l1.First.GetDefinition()?.Dominance == DominanceType.Recessive) ||
                                     (l1.Second.Symbol != "_" && l1.Second.GetDefinition()?.Dominance == DominanceType.Recessive) ||
                                     (l2.First.Symbol != "_" && l2.First.GetDefinition()?.Dominance == DominanceType.Recessive) ||
                                     (l2.Second.Symbol != "_" && l2.Second.GetDefinition()?.Dominance == DominanceType.Recessive);

            List<Allele> GetGametes(Locus locus, bool forceExpandUnknown = false)
            {
                var gametes = new List<Allele>();
                var locusSymbol = locus.GetLocusSymbol();
                var locusDef = GeneticParser.Definitions.FirstOrDefault(d => d.Symbol == locusSymbol);
                bool hasStacking = locusDef?.Alleles.Any(a => a.Dominance == DominanceType.PartiallyDominant || a.Dominance == DominanceType.PartiallyRecessive) ?? false;

                foreach (var a in new[] { locus.First, locus.Second })
                {
                    if (a.Symbol == "_" )
                    {
                        var candidates = new List<string>();
                        if (a.Suspected is { Count: > 0 })
                        {
                            candidates.AddRange(a.Suspected);
                        }
                        else if (locusDef != null && (hasStacking || forceExpandUnknown || (locusSymbol == "E" && (locus.First.Symbol == "E" || locus.Second.Symbol == "E" || locus.First.Symbol == "ej" || locus.Second.Symbol == "ej"))))
                        {
                            // For stacking loci, or the E locus when it has a dominant E, expand unknown slot to all possible valid alleles
                            // ALSO expand if forceExpandUnknown is set (used when the OTHER parent has a known recessive)
                            // If the OTHER allele is known, this unknown allele cannot be more dominant than it.
                            var other = a == locus.First ? locus.Second : locus.First;
                            if (!other.IsUnknown)
                            {
                                var otherDef = locusDef.Alleles.FirstOrDefault(al => al.Symbol == other.Symbol);
                                if (otherDef != null)
                                {
                                    // Add all alleles that are equal or less dominant than the known one
                                    candidates.AddRange(locusDef.Alleles
                                        .Where(al => al.Order >= otherDef.Order)
                                        .Select(al => al.Symbol));
                                }
                            }
                            else
                            {
                                // Both are unknown? Expand to ALL.
                                candidates.AddRange(locusDef.Alleles.Select(al => al.Symbol));
                            }
                        }

                        if (candidates.Count > 0)
                        {
                            foreach (var s in candidates)
                            {
                                if (a.Excluded != null && a.Excluded.Contains(s)) continue;
                                gametes.Add(new Allele(s));
                            }
                        }
                        else
                        {
                            gametes.Add(a);
                        }
                    }
                    else
                    {
                        gametes.Add(a);
                    }
                }
                return gametes;
            }

            var g1 = GetGametes(l1, forceExpandUnknown: hasKnownRecessive);
            var g2 = GetGametes(l2, forceExpandUnknown: hasKnownRecessive);

            // If a locus results in 100% of the same thing (e.g. _ x _ -> __), 
            // and it is effectively "unknown" in both parents, we can treat it as a single unit
            // to prevent the Cartesian product from exploding.
            bool IsEffectivelyUnknown(Locus l) => l.First.IsUnknown && l.Second.IsUnknown && 
                                               (l.First.Suspected == null || l.First.Suspected.Count == 0) &&
                                               (l.Second.Suspected == null || l.Second.Suspected.Count == 0);

            // SPECIAL CASE: If both have an unknown slot, but we are looking at something like B_ x B_,
            // we should only skip expansion IF it's a simple dominant/recessive locus.
            // If it's a stacking locus (PartiallyDominant/PartiallyRecessive), the unknown slot matters 
            // because it could contain another stacking allele.
            var def = GeneticParser.Definitions.FirstOrDefault(d => d.Symbol == symbol);
            bool hasStacking = def?.Alleles.Any(a => a.Dominance == DominanceType.PartiallyDominant || a.Dominance == DominanceType.PartiallyRecessive) ?? false;
            
            // ALSO, for the E locus, if we have E_, we want to see if it carries ej or e.
            bool isEWithDominant = symbol == "E" && (l1.First.Symbol == "E" || l1.Second.Symbol == "E" || l2.First.Symbol == "E" || l2.Second.Symbol == "E" ||
                                                    l1.First.Symbol == "ej" || l1.Second.Symbol == "ej" || l2.First.Symbol == "ej" || l2.Second.Symbol == "ej");
            
            // Do NOT expand simple dominant/recessive loci like B or D if one allele is dominant and other is unknown (e.g. B_)
            // unless there are constraints.
            bool isSimpleDominantWithUnknown = !hasStacking && symbol != "E" &&
                                               ((l1.First.Symbol != "_" && l1.Second.Symbol == "_") || 
                                                (l1.First.Symbol == "_" && l1.Second.Symbol != "_") ||
                                                (l2.First.Symbol != "_" && l2.Second.Symbol == "_") || 
                                                (l2.First.Symbol == "_" && l2.Second.Symbol != "_"));

            if (IsEffectivelyUnknown(l1) && IsEffectivelyUnknown(l2))
            {
                // Only skip if not E or other stacking locus
                if (!hasStacking && !isEWithDominant)
                {
                    locusPossibilities[symbol] = new List<(Locus Locus, double Probability)> { (l1, 1.0) };
                    continue;
                }
            }
            
            // Also handle cases like B_ x B_ where we just want to see B_ (or its resolution) 
            // instead of expanding every possibility if not requested.
            bool IsPartiallyUnknown(Locus l) => (l.First.IsUnknown || l.Second.IsUnknown) &&
                                               (l.First.Suspected == null || l.First.Suspected.Count == 0) &&
                                               (l.Second.Suspected == null || l.Second.Suspected.Count == 0);

            if (IsPartiallyUnknown(l1) && IsPartiallyUnknown(l2) && l1.Matches(l2))
            {
                // Also, ONLY skip if NO explicitly specified suspects/exclusions exist in the unknown slot.
                bool hasConstraints = l1.First.Suspected?.Count > 0 || l1.First.Excluded?.Count > 0 ||
                                     l1.Second.Suspected?.Count > 0 || l1.Second.Excluded?.Count > 0 ||
                                     l2.First.Suspected?.Count > 0 || l2.First.Excluded?.Count > 0 ||
                                     l2.Second.Suspected?.Count > 0 || l2.Second.Excluded?.Count > 0;

                if (!hasStacking && !hasConstraints && !isEWithDominant)
                {
                    locusPossibilities[symbol] = new List<(Locus Locus, double Probability)> { (l1, 1.0) };
                    continue;
                }
            }

            // Further restrict expansion for simple loci when only one parent has an unknown slot
            if (isSimpleDominantWithUnknown && !isEWithDominant)
            {
                bool hasConstraints = l1.First.Suspected?.Count > 0 || l1.First.Excluded?.Count > 0 ||
                                     l1.Second.Suspected?.Count > 0 || l1.Second.Excluded?.Count > 0 ||
                                     l2.First.Suspected?.Count > 0 || l2.First.Excluded?.Count > 0 ||
                                     l2.Second.Suspected?.Count > 0 || l2.Second.Excluded?.Count > 0;

                if (!hasConstraints && !hasKnownRecessive)
                {
                    // For simple dominant/recessive loci like B or D, if we have B_ x BB, 
                    // we don't need to assume the B_ parent carries b.
                    
                    locusPossibilities[symbol] = new List<(Locus Locus, double Probability)> { (new Locus(l1.First.IsUnknown ? l2.First : l1.First, new Allele("_")) { OverrideLocusSymbol = symbol }, 1.0) };
                    continue;
                }
            }

            var outcomes = new Dictionary<string, (Locus Locus, int Count)>();
            int total = g1.Count * g2.Count;

            foreach (var a1 in g1)
            {
                foreach (var a2 in g2)
                {
                    var offspringLocus = new Locus(a1, a2) { OverrideLocusSymbol = symbol }.Normalize();
                    
                    // Phenotypic reduction: 
                    // Consolidation of variations: Dominant genes are always dominant, so what the recessive is doesn't matter.
                    // Stacking genes (partially dominant/recessive) can have their expression modified by the recessive.
                    if (!offspringLocus.First.IsUnknown && !offspringLocus.Second.IsUnknown)
                    {
                        var d1 = offspringLocus.First.GetDefinition();
                        var d2 = offspringLocus.Second.GetDefinition();
                        
                        if (d1 != null && d2 != null)
                        {
                            bool d1IsStacking = d1.Dominance == DominanceType.PartiallyDominant || d1.Dominance == DominanceType.PartiallyRecessive;
                            bool d2IsStacking = d2.Dominance == DominanceType.PartiallyDominant || d2.Dominance == DominanceType.PartiallyRecessive;

                            if (d1.Dominance == DominanceType.Dominant)
                            {
                                // Fully dominant gene masks everything
                                var reducedLocus = new Locus(offspringLocus.First, new Allele("_")) { OverrideLocusSymbol = symbol };

                                // CHECK: Does homozygous form match a DIFFERENT specific variety?
                                var currentFull = new RabbitGenotype();
                                currentFull.Loci.AddRange(allSymbols.Select(s => s == symbol ? offspringLocus : (p1Loci.GetValueOrDefault(s) ?? new Locus(new Allele("_"), new Allele("_")) { OverrideLocusSymbol = s })));

                                var reducedFull = new RabbitGenotype();
                                reducedFull.Loci.AddRange(allSymbols.Select(s => s == symbol ? reducedLocus : (p1Loci.GetValueOrDefault(s) ?? new Locus(new Allele("_"), new Allele("_")) { OverrideLocusSymbol = s })));

                                string currentName = VarietyService.IdentifyDescription(currentFull).Split('(')[0].Trim();
                                string reducedName = VarietyService.IdentifyDescription(reducedFull).Split('(')[0].Trim();

                                if (currentName == reducedName || reducedName.Contains("Unknown"))
                                {
                                    offspringLocus = reducedLocus;
                                }
                            }
                            else if (d1.Dominance == DominanceType.Recessive)
                            {
                                // Recessive gene only expresses as homozygous. 
                                // (By normalization, if First is Recessive, Second must be Recessive too or Unknown)
                                // We keep it homozygous for display.
                            }
                            else if (offspringLocus.First.Symbol == "b" && offspringLocus.Second.Symbol == "b")
                            {
                                // User explicitly requested: "chocolate is always explicitly 'bb'"
                                // So do not reduce bb to b_.
                            }
                            else
                            {
                                // Partially dominant/recessive (stacking) genes.
                                // If the first allele is a stacking gene, and the second one is less dominant,
                                // we should reduce it to Symbol_ IF it phenotypically masks the second one.
                                // The user explicitly mentioned:
                                // - at_ (at over a)
                                // - cchl_ (cchl over ch or c)
                                // - ej_ (ej over e)
                                // - E_ (E over ej or e)
                                
                                bool shouldReduce = false;
                                if (offspringLocus.First.Symbol == offspringLocus.Second.Symbol)
                                {
                                    shouldReduce = true;
                                }
                                else if (d1.Order < d2.Order)
                                {
                                    // First is more dominant.
                                    // Reduction rules:
                                    // 1. If d2 is the bottom recessive of the locus, it's usually masked.
                                    var locusDef = GeneticParser.Definitions.FirstOrDefault(def => def.Symbol == symbol);
                                    var bottomRecessive = locusDef?.Alleles.OrderByDescending(a => a.Order).FirstOrDefault();
                                    
                                    if (bottomRecessive != null && offspringLocus.Second.Symbol == bottomRecessive.Symbol)
                                    {
                                        shouldReduce = true;
                                    }
                                    // 2. Specific case: cchl masks ch (as per user: "All cchl, ch, or c recessives fall under the same shagouti term")
                                    else if (offspringLocus.First.Symbol == "cchl" && (offspringLocus.Second.Symbol == "ch" || offspringLocus.Second.Symbol == "c"))
                                    {
                                        shouldReduce = true;
                                    }
                                    // 3. Specific case: E masks ej
                                    else if (offspringLocus.First.Symbol == "E" && offspringLocus.Second.Symbol == "ej")
                                    {
                                        shouldReduce = true;
                                    }
                                }

                                if (shouldReduce)
                                {
                                    // CHECK: Does homozygous form match a DIFFERENT specific variety?
                                    // If so, we must respect that explicit definition as requested by user.
                                    var reducedLocus = new Locus(offspringLocus.First, new Allele("_")) { OverrideLocusSymbol = symbol };

                                    // Create full genotypes to check variety identification
                                    var currentFull = new RabbitGenotype();
                                    currentFull.Loci.AddRange(allSymbols.Select(s => s == symbol ? offspringLocus : (p1Loci.GetValueOrDefault(s) ?? new Locus(new Allele("_"), new Allele("_")) { OverrideLocusSymbol = s })));

                                    var reducedFull = new RabbitGenotype();
                                    reducedFull.Loci.AddRange(allSymbols.Select(s => s == symbol ? reducedLocus : (p1Loci.GetValueOrDefault(s) ?? new Locus(new Allele("_"), new Allele("_")) { OverrideLocusSymbol = s })));

                                    string currentName = VarietyService.IdentifyDescription(currentFull).Split('(')[0].Trim();
                                    string reducedName = VarietyService.IdentifyDescription(reducedFull).Split('(')[0].Trim();

                                    if (currentName != reducedName && !reducedName.Contains("Unknown"))
                                    {
                                        // They represent different varieties, do not reduce.
                                        shouldReduce = false;
                                    }
                                }

                                if (shouldReduce)
                                {
                                    offspringLocus = new Locus(offspringLocus.First, new Allele("_")) { OverrideLocusSymbol = symbol };
                                }
                            }
                        }
                    }
                    else if (!offspringLocus.First.IsUnknown && offspringLocus.Second.IsUnknown)
                    {
                        // Already in consolidated form (e.g. A_)
                    }

                    // Normalize suspects/exclusions for grouping
                    // If we have A_ and Aat, both should become A_ for grouping.
                    // We already did this above for AA and Aat. 
                    // What about A(at) vs A_?
                    if (!offspringLocus.First.IsUnknown && offspringLocus.Second.IsUnknown && (offspringLocus.Second.Suspected?.Count > 0 || offspringLocus.Second.Excluded?.Count > 0))
                    {
                         // If we are grouping by phenotype, A(at) is phenotypically same as A_.
                         // However, if the first allele is a "stacking" gene, the second one MIGHT matter.
                         var d1 = offspringLocus.First.GetDefinition();
                         bool isStacking = d1 != null && (d1.Dominance == DominanceType.PartiallyDominant || d1.Dominance == DominanceType.PartiallyRecessive);

                         if (!isStacking)
                         {
                             offspringLocus = new Locus(offspringLocus.First, new Allele("_")) { OverrideLocusSymbol = symbol };
                         }
                    }
                    
                    var key = offspringLocus.ToString();
                    if (outcomes.TryGetValue(key, out var existing))
                    {
                        outcomes[key] = (existing.Locus, existing.Count + 1);
                    }
                    else
                    {
                        outcomes[key] = (offspringLocus, 1);
                    }
                }
            }

            var possibilities = outcomes.Values
                .Select(v => (v.Locus, (double)v.Count / total))
                .OrderByDescending(p => p.Item2)
                .ToList();

            // If a locus is completely unknown in both parents, avoid expanding it into every possible allele
            // to prevent probability dilution, unless the user explicitly wants to explore it.
            // We'll keep it as __ (original) with 100% probability for the purpose of prediction.
            if ((l1.First.IsUnknown && l1.Second.IsUnknown && l1.First.Suspected == null && l1.Second.Suspected == null) &&
                (l2.First.IsUnknown && l2.Second.IsUnknown && l2.First.Suspected == null && l2.Second.Suspected == null))
            {
                possibilities = new List<(Locus Locus, double Probability)> { (l1, 1.0) };
            }

            locusPossibilities[symbol] = possibilities;
        }

        // Cartesian product of all locus possibilities
        var currentPredictions = new List<OffspringPrediction> 
        { 
            new OffspringPrediction(new RabbitGenotype(), 1.0) 
        };

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

            // Group by genotype string during intermediate steps to prevent explosion 
            // but preserve genetic information. We do NOT apply a limit here yet.
            currentPredictions = nextPredictions
                .GroupBy(p => p.Genotype.ToString())
                .Select(g => new OffspringPrediction(g.First().Genotype, g.Sum(p => p.Probability)))
                .OrderByDescending(p => p.Probability)
                .ToList();
        }

        // Final consolidation: Group by variety description if identifyFunc is provided.
        // Otherwise, group by genotype.
            var finalResults = currentPredictions
                .GroupBy(p => identifyFunc != null ? identifyFunc(p.Genotype) : p.Genotype.ToString())
                .Select(g =>
                {
                    // For the displayed genotype, we want the most "representative" one.
                    // If we have A_, we prefer that over Aa for phenotypic display if they both map to Chestnut.
                    // Actually, CountKnownAlleles was used to pick the one with MOST known alleles.
                    // But here, if we consolidated into a phenotype, the genotype should reflect that phenotype.
                    
                    // Let's try to find a genotype that has the same string representation as the key
                    // (if identifyFunc was null) or just pick the best one.
                    var best = g.OrderByDescending(x => CountKnownAlleles(x.Genotype)).First();
                    return new OffspringPrediction(best.Genotype, g.Sum(p => p.Probability));
                })
                .OrderByDescending(p => p.Probability)
                .Take(limit) // Limit only the final listing
                .ToList();

        return finalResults;
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
                if (allele.Suspected is { Count: > 0 })
                {
                    candidates.AddRange(allele.Suspected);
                }
                else
                {
                    candidates.AddRange(allPossibleAlleles);
                    if (candidates.Count == 0) candidates.Add("_");
                }

                foreach (var s in candidates)
                {
                    if (allele.Excluded != null && allele.Excluded.Contains(s)) continue;
                    options.Add(new Allele(s));
                }

                if (options.Count == 0) options.Add(new Allele("_"));
            }
            else
            {
                options.Add(allele);
            }
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
            if (p.First.Suspected != null) 
            {
                foreach (var s in p.First.Suspected) 
                {
                    if (p.First.Excluded != null && p.First.Excluded.Contains(s)) continue;
                    options.Add(s);
                }
            }
            if (p.Second.Suspected != null) 
            {
                foreach (var s in p.Second.Suspected) 
                {
                    if (p.Second.Excluded != null && p.Second.Excluded.Contains(s)) continue;
                    options.Add(s);
                }
            }
            
            // If there's an underscore, it could be any allele seen in children or the other parent
            if (p.First.IsUnknown || p.Second.IsUnknown)
            {
                var pExcluded = new HashSet<string>();
                if (p.First.Excluded != null) foreach (var e in p.First.Excluded) pExcluded.Add(e);
                if (p.Second.Excluded != null) foreach (var e in p.Second.Excluded) pExcluded.Add(e);

                foreach (var ka in knownAlleles)
                {
                    if (!pExcluded.Contains(ka)) options.Add(ka);
                }
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
