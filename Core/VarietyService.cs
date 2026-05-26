using System.Text.Json;

namespace RabbitGeneProcessor.Core;

/// <summary>
/// Service for managing rabbit variety definitions.
/// </summary>
public static class VarietyService
{
    private static List<VarietyDefinition>? _varieties;
    private static List<VarietyDefinition>? _modifiers;
    private static List<VarietyDefinition>? _breeds;

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
        _breeds = JsonSerializer.Deserialize<List<VarietyDefinition>>(breedsJson, options);
    }

    public static List<VarietyDefinition> Varieties => _varieties ?? throw new InvalidOperationException("VarietyService not initialized.");
    public static List<VarietyDefinition> Modifiers => _modifiers ?? throw new InvalidOperationException("VarietyService not initialized.");
    public static List<VarietyDefinition> Breeds => _breeds ?? throw new InvalidOperationException("VarietyService not initialized.");

    /// <summary>
    /// Parses a descriptive string into a breed, variety, and list of modifiers.
    /// Example: "Broken VM Chestnut Rex" -> Breed: Rex, Variety: Chestnut, Modifiers: [Broken, VM]
    /// </summary>
    public static (VarietyDefinition Breed, VarietyDefinition Variety, List<VarietyDefinition> Modifiers) ParseDescription(string description)
    {
        var words = description.Split(new[] { ' ', '-' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var remainingWords = words.ToList();

        VarietyDefinition? foundBreed = null;
        VarietyDefinition? foundVariety = null;
        var foundModifiers = new List<VarietyDefinition>();

        // We need to match multi-word names too. Let's try matching from longest possible phrases down to single words.
        
        void FindMatches<T>(List<T> definitions, Action<T> onFound) where T : VarietyDefinition
        {
            for (int length = remainingWords.Count; length > 0; length--)
            {
                for (int start = 0; start <= remainingWords.Count - length; start++)
                {
                    var phrase = string.Join(" ", remainingWords.Skip(start).Take(length));
                    var match = definitions.FirstOrDefault(d => 
                        d.Name.Equals(phrase, StringComparison.OrdinalIgnoreCase) || 
                        (d.AlternateNames != null && d.AlternateNames.Any(a => a.Equals(phrase, StringComparison.OrdinalIgnoreCase))));

                    if (match != null)
                    {
                        onFound(match);
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
        FindMatches(Varieties, v => foundVariety = v);
        FindMatches(Modifiers, m => foundModifiers.Add(m));

        if (foundBreed == null) throw new ArgumentException($"Could not identify breed in description: {description}");
        if (foundVariety == null) throw new ArgumentException($"Could not identify variety in description: {description}");

        return (foundBreed, foundVariety, foundModifiers);
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

        void ApplyGenotype(string genotypeString)
        {
            var genotype = RabbitGenotype.Parse(genotypeString);
            foreach (var locus in genotype.Loci)
            {
                var symbol = locus.GetLocusSymbol();
                combinedLoci[symbol] = locus;
            }
        }

        // 1. Apply breed-specific genes first
        ApplyGenotype(breed.GenotypeString);

        // 2. Apply default variety
        ApplyGenotype(variety.GenotypeString);

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
                    ApplyGenotype(modifier.GenotypeString);
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

        return string.Join(",", combinedLoci.Values);
    }

    /// <summary>
    /// Attempts to identify the breed, variety, and modifiers for a given genotype.
    /// </summary>
    public static string IdentifyDescription(RabbitGenotype genotype)
    {
        VarietyDefinition? bestBreed = null;
        VarietyDefinition? bestVariety = null;
        var appliedModifiers = new List<VarietyDefinition>();

        // 1. Identify Breed
        // Breeds usually have specific genes (like ll for Angora).
        // We look for the breed that has the most matching loci.
        int maxBreedMatches = -1;
        foreach (var breed in Breeds)
        {
            var breedGenotype = RabbitGenotype.Parse(breed.GenotypeString);
            if (genotype.Contains(breedGenotype))
            {
                int specificity = CountSpecificity(breedGenotype);
                if (specificity > maxBreedMatches)
                {
                    maxBreedMatches = specificity;
                    bestBreed = breed;
                }
            }
        }

        // 2. Identify Variety
        int maxVarietyMatches = -1;
        foreach (var variety in Varieties)
        {
            var varietyGenotype = RabbitGenotype.Parse(variety.GenotypeString);
            if (genotype.Contains(varietyGenotype))
            {
                // We want the most specific variety.
                int specificity = CountSpecificity(varietyGenotype);
                if (specificity > maxVarietyMatches)
                {
                    maxVarietyMatches = specificity;
                    bestVariety = variety;
                }
            }
        }

        if (bestVariety == null) return "Unknown Variety";

        // 3. Identify Modifiers
        var baseGenotypeString = (bestBreed?.GenotypeString ?? "") + "," + (bestVariety?.GenotypeString ?? "");
        var baseGenotype = RabbitGenotype.Parse(baseGenotypeString);

        foreach (var modifier in Modifiers)
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
                        continue;
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
                if (appliedModifiers.Any(m => m.GenotypeString == modifier.GenotypeString))
                    continue;

                appliedModifiers.Add(modifier);
            }
        }

        if (bestVariety == null) return "Unknown Variety";

        var description = bestVariety.Name;
        
        // Apply prefix modifiers
        foreach (var mod in appliedModifiers.Where(m => m.Type == ModifierType.Prefix))
        {
            if (!description.Contains(mod.Name))
                description = mod.Name + " " + description;
        }

        // Apply breed as prefix (most common)
        if (bestBreed != null)
        {
            if (!description.Contains(bestBreed.Name))
                description = bestBreed.Name + " " + description;
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
