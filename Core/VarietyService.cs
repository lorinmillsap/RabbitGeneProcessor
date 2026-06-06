using System.Text.Json;

namespace RabbitGeneProcessor.Core;

/// <summary>
/// Service for managing rabbit variety definitions.
/// </summary>
public static class VarietyService
{
    private static List<VarietyDefinition>? _varieties;
    private static List<VarietyDefinition>? _modifiers;
    private static List<BreedDefinition>? _breeds;

    public static void Initialize(string varietiesPath, string modifiersPath, string breedsPath)
    {
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
            Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
        };

        var varietiesJson = File.ReadAllText(varietiesPath);
        _varieties = JsonSerializer.Deserialize<List<VarietyDefinition>>(varietiesJson, options);

        var modifiersJson = File.ReadAllText(modifiersPath);
        _modifiers = JsonSerializer.Deserialize<List<VarietyDefinition>>(modifiersJson, options);

        var breedsJson = File.ReadAllText(breedsPath);
        _breeds = JsonSerializer.Deserialize<List<BreedDefinition>>(breedsJson, options);
    }

    public static List<VarietyDefinition> Varieties => _varieties ?? throw new InvalidOperationException("VarietyService not initialized.");
    public static List<VarietyDefinition> Modifiers => _modifiers ?? throw new InvalidOperationException("VarietyService not initialized.");
    public static List<BreedDefinition> Breeds => _breeds ?? throw new InvalidOperationException("VarietyService not initialized.");

    /// <summary>
    /// Parses a descriptive string into a breed, variety, and list of modifiers.
    /// Example: "Broken VM Chestnut Rex" -> Breed: Rex, Variety: Chestnut, Modifiers: [Broken, VM]
    /// </summary>
    public static (BreedDefinition? Breed, VarietyDefinition Variety, List<VarietyDefinition> Modifiers) ParseDescription(string description)
    {
        var words = description.Split(new[] { ' ', '-' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var remainingWords = words.ToList();

        BreedDefinition? foundBreed = null;
        VarietyDefinition? foundVariety = null;
        var foundModifiers = new List<VarietyDefinition>();

        // We need to match multi-word names too. Let's try matching from longest possible phrases down to single words.
        
        void FindMatches<T>(List<T> definitions, Action<T> onFound) where T : class
        {
            for (int length = remainingWords.Count; length > 0; length--)
            {
                for (int start = 0; start <= remainingWords.Count - length; start++)
                {
                    var phrase = string.Join(" ", remainingWords.Skip(start).Take(length));
                    
                    // Match based on type
                    object? match = null;
                    if (typeof(T) == typeof(BreedDefinition))
                    {
                        match = ((List<BreedDefinition>)(object)definitions).FirstOrDefault(d => 
                            d.Name.Equals(phrase, StringComparison.OrdinalIgnoreCase) || 
                            (d.AlternateNames != null && d.AlternateNames.Any(a => a.Equals(phrase, StringComparison.OrdinalIgnoreCase))));
                    }
                    else if (typeof(T) == typeof(VarietyDefinition))
                    {
                        match = ((List<VarietyDefinition>)(object)definitions).FirstOrDefault(d => 
                            d.Name.Equals(phrase, StringComparison.OrdinalIgnoreCase) || 
                            (d.AlternateNames != null && d.AlternateNames.Any(a => a.Equals(phrase, StringComparison.OrdinalIgnoreCase))));
                    }

                    if (match != null)
                    {
                        onFound((T)match);
                        remainingWords.RemoveRange(start, length);
                        // Reset loops as remainingWords changed
                        length = remainingWords.Count + 1; 
                        break;
                    }
                }
            }
        }

        // Search order: Breeds, then Varieties, then Modifiers
        FindMatches(Breeds, b => foundBreed = b);
        
        // When searching for varieties, if a breed was found, prioritize breed-specific varieties
        if (foundBreed != null && foundBreed.Varieties != null)
        {
            FindMatches(foundBreed.Varieties, v => foundVariety = v);
        }

        if (foundVariety == null)
        {
            // Fallback to searching all varieties, but prioritize those containing the breed name if applicable
            if (foundBreed != null)
            {
                var breedSpecificVarieties = Varieties.Where(v => v.Name.Contains(foundBreed.Name, StringComparison.OrdinalIgnoreCase)).ToList();
                FindMatches(breedSpecificVarieties, v => foundVariety = v);
            }
            
            if (foundVariety == null)
            {
                FindMatches(Varieties, v => foundVariety = v);
            }
        }
        
        FindMatches(Modifiers, m => foundModifiers.Add(m));

        if (foundVariety == null) throw new ArgumentException($"Could not identify variety in description: {description}");

        return (foundBreed, foundVariety, foundModifiers);
    }

    /// <summary>
    /// Calculates the genotype from a descriptive string.
    /// Example: "Broken VM Chestnut Rex" -> Returns calculated genotype string.
    /// </summary>
    public static string CalculateGenotypeFromDescription(string description)
    {
        var (breed, variety, modifiers) = ParseDescription(description);
        
        var combinedLoci = new Dictionary<string, Locus>();

        void ApplyGenotype(string genotypeString, bool forceOverride = false)
        {
            var genotype = RabbitGenotype.Parse(genotypeString);
            foreach (var locus in genotype.Loci)
            {
                var symbol = locus.GetLocusSymbol();
                if (combinedLoci.TryGetValue(symbol, out var existing))
                {
                    combinedLoci[symbol] = existing.Combine(locus, forceOverride);
                }
                else
                {
                    combinedLoci[symbol] = locus;
                }
            }
        }

        // 0. Initialize A, B, C, D, E, and En loci as blanks (__)
        var primaryLociSymbols = new[] { "A", "B", "C", "D", "E", "En" };
        foreach (var symbol in primaryLociSymbols)
        {
            combinedLoci[symbol] = new Locus(new Allele("_"), new Allele("_"));
        }

        // 1. Apply default variety
        ApplyGenotype(variety.GenotypeString);

        // 2. Apply breed-specific genes (which override variety)
        if (breed != null)
        {
            ApplyGenotype(breed.GenotypeString, forceOverride: true);
        }

        // 3. Apply modifiers (which will override)
        foreach (var modifier in modifiers)
        {
            ApplyGenotype(modifier.GenotypeString, forceOverride: true);
        }

        // 4. Apply exclusion strings for modifiers that are NOT present (passive filters)
        foreach (var modifier in Modifiers)
        {
            if (!modifiers.Contains(modifier) && !string.IsNullOrEmpty(modifier.ExclusionGenotypeString))
            {
                // Passive filters should NOT override breed-specific requirements
                // We'll apply them without forceOverride
                ApplyGenotype(modifier.ExclusionGenotypeString);
            }
        }

        // Return sorted by locus symbol
        var sortedLoci = combinedLoci.OrderBy(kvp => kvp.Key).Select(kvp => kvp.Value);
        return string.Join(",", sortedLoci);
    }

    /// <summary>
    /// Gets the full genotype string for a breed and variety, optionally applying modifiers.
    /// Uses ExclusionGenotypeStrings from modifiers to determine default values for omitted modifiers.
    /// </summary>
    public static string GetFullGenotypeString(string breedName, string varietyName, List<string>? modifierNames = null)
    {
        var breed = Breeds.FirstOrDefault(b => 
            b.Name.Equals(breedName, StringComparison.OrdinalIgnoreCase) || 
            (b.AlternateNames != null && b.AlternateNames.Any(a => a.Equals(breedName, StringComparison.OrdinalIgnoreCase))))
                      ?? throw new ArgumentException($"Breed '{breedName}' not found.", nameof(breedName));

        var variety = Varieties.FirstOrDefault(v => 
            v.Name.Equals(varietyName, StringComparison.OrdinalIgnoreCase) || 
            (v.AlternateNames != null && v.AlternateNames.Any(a => a.Equals(varietyName, StringComparison.OrdinalIgnoreCase))))
                      ?? throw new ArgumentException($"Variety '{varietyName}' not found.", nameof(varietyName));

        var combinedLoci = new Dictionary<string, Locus>();

        void ApplyGenotype(string genotypeString, bool forceOverride = false)
        {
            var genotype = RabbitGenotype.Parse(genotypeString);
            foreach (var locus in genotype.Loci)
            {
                var symbol = locus.GetLocusSymbol();
                if (combinedLoci.TryGetValue(symbol, out var existing))
                {
                    combinedLoci[symbol] = existing.Combine(locus, forceOverride);
                }
                else
                {
                    combinedLoci[symbol] = locus;
                }
            }
        }

        // 0. Initialize A, B, C, D, E, and En loci as blanks (__)
        var primaryLociSymbols = new[] { "A", "B", "C", "D", "E", "En" };
        foreach (var symbol in primaryLociSymbols)
        {
            combinedLoci[symbol] = new Locus(new Allele("_"), new Allele("_"));
        }

        // 1. Apply default variety
        ApplyGenotype(variety.GenotypeString);

        // 2. Apply breed-specific genes (which override variety)
        ApplyGenotype(breed.GenotypeString, forceOverride: true);

        // 3. Apply modifiers (which will override)
        var appliedModifiers = new List<VarietyDefinition>();
        if (modifierNames != null)
        {
            foreach (var modName in modifierNames)
            {
                var modifier = Modifiers.FirstOrDefault(m => 
                    m.Name.Equals(modName, StringComparison.OrdinalIgnoreCase) || 
                    (m.AlternateNames != null && m.AlternateNames.Any(a => a.Equals(modName, StringComparison.OrdinalIgnoreCase))));
                
                if (modifier != null)
                {
                    ApplyGenotype(modifier.GenotypeString, forceOverride: true);
                    appliedModifiers.Add(modifier);
                }
            }
        }

        // 4. Apply exclusion strings for modifiers that are NOT present (passive filters)
        foreach (var modifier in Modifiers)
        {
            if (!appliedModifiers.Contains(modifier) && !string.IsNullOrEmpty(modifier.ExclusionGenotypeString))
            {
                ApplyGenotype(modifier.ExclusionGenotypeString);
            }
        }

        // Return sorted by locus symbol (optional but helpful for consistency)
        var sortedLoci = combinedLoci.OrderBy(kvp => kvp.Key).Select(kvp => kvp.Value);
        return string.Join(",", sortedLoci);
    }

    /// <summary>
    /// Attempts to identify the variety and modifiers for a given genotype.
    /// A breed can be optionally provided to refine the identification, 
    /// as breed cannot be determined from genotype alone.
    /// </summary>
    public static string IdentifyDescription(RabbitGenotype genotype, string? breedName = null)
    {
        BreedDefinition? breed = null;
        if (!string.IsNullOrEmpty(breedName))
        {
            breed = Breeds.FirstOrDefault(b => 
                b.Name.Equals(breedName, StringComparison.OrdinalIgnoreCase) || 
                (b.AlternateNames != null && b.AlternateNames.Any(a => a.Equals(breedName, StringComparison.OrdinalIgnoreCase))));
        }

        VarietyDefinition? bestVariety = null;
        var appliedModifiers = new List<VarietyDefinition>();

        // 1. Identify Variety
        int maxVarietyMatches = -1;
        var varietiesToSearch = Varieties.ToList();
        if (breed?.Varieties != null)
        {
            varietiesToSearch.AddRange(breed.Varieties);
        }

        foreach (var v in varietiesToSearch)
        {
            var varietyGenotype = RabbitGenotype.Parse(v.GenotypeString);
            if (genotype.Contains(varietyGenotype))
            {
                // We want the most specific variety.
                int specificity = CountSpecificity(varietyGenotype);
                
                // If a breed is provided, prioritize breed-specific varieties
                bool isBreedSpecific = breed != null && (breed.Varieties?.Contains(v) == true || v.Name.Contains(breed.Name, StringComparison.OrdinalIgnoreCase));
                if (isBreedSpecific) specificity += 100; // Boost specificity for breed-specific varieties

                if (specificity > maxVarietyMatches)
                {
                    maxVarietyMatches = specificity;
                    bestVariety = v;
                }
            }
        }

        if (bestVariety == null)
        {
            // If no variety matches (e.g., cc or vv which are incomplete),
            // try to find the variety that matches the MOST alleles.
            maxVarietyMatches = -1;
            foreach (var v in varietiesToSearch)
            {
                var varietyGenotype = RabbitGenotype.Parse(v.GenotypeString);
                // Count how many loci in varietyGenotype are satisfied by the input genotype
                int matches = 0;
                foreach (var vLocus in varietyGenotype.Loci)
                {
                    if (genotype.Contains(new RabbitGenotype { Loci = { vLocus } }))
                    {
                        matches += CountSpecificity(new RabbitGenotype { Loci = { vLocus } });
                    }
                }

                // Boost breed-specific fallback matches too
                bool isBreedSpecific = breed != null && breed.Varieties != null && breed.Varieties.Any(bv => bv.Name == v.Name && bv.GenotypeString == v.GenotypeString);
                if (isBreedSpecific) matches += 100;

                if (matches > maxVarietyMatches)
                {
                    maxVarietyMatches = matches;
                    bestVariety = v;
                }
            }
        }

        if (bestVariety == null) return "Unknown Variety";

        // 2. Identify Modifiers
        var baseGenotypeString = (breed?.GenotypeString ?? "") + "," + (bestVariety?.GenotypeString ?? "");
        var baseGenotype = RabbitGenotype.Parse(baseGenotypeString);

        // Sort modifiers by priority, then specificity so we can skip redundant ones (e.g. MM vs M_)
        // Higher priority (larger number) comes first.
        var sortedModifiers = Modifiers
            .OrderByDescending(m => m.Priority)
            .ThenByDescending(m => CountSpecificity(RabbitGenotype.Parse(m.GenotypeString)))
            .ToList();

        foreach (var modifier in sortedModifiers)
        {
            var modGenotype = RabbitGenotype.Parse(modifier.GenotypeString);
            
            // 1. Does the input genotype match this modifier?
            if (genotype.Contains(modGenotype))
            {
                // 2. Is this modifier already "covered" by the base variety/breed?
                if (baseGenotype.Contains(modGenotype))
                {
                    // Special case: if the modifier is more specific than the base (e.g., MM vs M_)
                    if (CountSpecificity(modGenotype) <= CountSpecificity(baseGenotype, modGenotype))
                    {
                        // EXCEPTION: "Gold Tipped" and "Silver Tipped" are intended to be explicit when 
                        // used with Steel varieties, even if C_ or cchd_ is already in the base.
                        if (modifier.Name != "Gold Tipped" && modifier.Name != "Silver Tipped")
                            continue;
                        
                        // Only add them if Steel genes are present
                        if (!genotype.Loci.Any(l => l.GetLocusSymbol() == "E" && l.ToString().Contains("Es")))
                             continue;
                    }
                }

                // 3. Special case: if the input genotype matches the EXCLUSION string of this modifier,
                // we should NOT add the modifier name.
                if (!string.IsNullOrEmpty(modifier.ExclusionGenotypeString))
                {
                    var exclusionGenotype = RabbitGenotype.Parse(modifier.ExclusionGenotypeString);
                    if (genotype.Contains(exclusionGenotype))
                        continue;
                }

                // 4. Avoid double counting identical genotypes (VM/VC)
                // Also avoid including "Non-Vienna" if other Vienna genes are present.
                if (appliedModifiers.Any(m => m.GenotypeString == modifier.GenotypeString))
                    continue;

                // Avoid redundant modifiers if a more specific one on the same locus is already applied
                if (appliedModifiers.Any(m => 
                {
                    var existingModGenotype = RabbitGenotype.Parse(m.GenotypeString);
                    return existingModGenotype.Contains(modGenotype) && CountSpecificity(existingModGenotype) > CountSpecificity(modGenotype);
                }))
                {
                    continue;
                }

                if (modifier.Name == "Non-Vienna" && genotype.Loci.Any(l => l.GetLocusSymbol() == "V" && (l.First.Symbol == "v" || l.Second.Symbol == "v")))
                    continue;

                appliedModifiers.Add(modifier);
            }
        }

        var description = bestVariety?.Name ?? "Unknown Variety";
        
        // Apply prefix modifiers
        foreach (var mod in appliedModifiers.Where(m => m.Type == ModifierType.Prefix))
        {
            if (!description.Contains(mod.Name))
                description = mod.Name + " " + description;
        }

        // Apply breed as prefix if provided
        if (breed != null)
        {
            if (!description.Contains(breed.Name))
                description = breed.Name + " " + description;
        }

        // Apply suffix modifiers
        foreach (var mod in appliedModifiers.Where(m => m.Type == ModifierType.Suffix))
        {
            if (!description.Contains(mod.Name))
                description = description + " " + mod.Name;
        }

        return description;
    }

    private static int CountSpecificity(RabbitGenotype genotype, RabbitGenotype? filterBy = null)
    {
        int score = 0;
        var filterLoci = filterBy?.Loci.ToDictionary(l => l.GetLocusSymbol()) ?? new Dictionary<string, Locus>();

        foreach (var locus in genotype.Loci)
        {
            var symbol = locus.GetLocusSymbol();
            if (filterBy != null && !filterLoci.ContainsKey(symbol)) continue;

            if (locus.First.Symbol != "_") score++;
            if (locus.Second.Symbol != "_") score++;
        }
        return score;
    }
}
