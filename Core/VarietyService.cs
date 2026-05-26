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

        var genotypeParts = new List<string> { breed.GenotypeString, variety.GenotypeString };
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
                    genotypeParts.Add(modifier.GenotypeString);
                    appliedModifiers.Add(modifier);
                }
            }
        }

        // Apply exclusion strings for modifiers that are NOT present
        foreach (var modifier in Modifiers)
        {
            if (!appliedModifiers.Contains(modifier) && !string.IsNullOrEmpty(modifier.ExclusionGenotypeString))
            {
                genotypeParts.Add(modifier.ExclusionGenotypeString);
            }
        }

        return string.Join(",", genotypeParts);
    }
}
