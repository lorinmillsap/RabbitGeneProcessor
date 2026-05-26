using System.Text.Json;

namespace RabbitGeneProcessor.Core;

/// <summary>
/// Service for managing rabbit variety definitions.
/// </summary>
public static class VarietyService
{
    private static List<VarietyDefinition>? _varieties;
    private static List<VarietyDefinition>? _modifiers;

    public static void Initialize(string varietiesPath, string modifiersPath)
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
    }

    public static List<VarietyDefinition> Varieties => _varieties ?? throw new InvalidOperationException("VarietyService not initialized.");
    public static List<VarietyDefinition> Modifiers => _modifiers ?? throw new InvalidOperationException("VarietyService not initialized.");

    /// <summary>
    /// Gets the full genotype string for a variety, optionally applying modifiers.
    /// Uses ExclusionGenotypeStrings from modifiers to determine default values for omitted modifiers.
    /// </summary>
    public static string GetFullGenotypeString(string varietyName, List<string>? modifierNames = null)
    {
        var variety = Varieties.FirstOrDefault(v => 
            v.Name.Equals(varietyName, StringComparison.OrdinalIgnoreCase) || 
            (v.AlternateNames != null && v.AlternateNames.Any(a => a.Equals(varietyName, StringComparison.OrdinalIgnoreCase))))
                      ?? throw new ArgumentException($"Variety '{varietyName}' not found.", nameof(varietyName));

        var genotypeParts = new List<string> { variety.GenotypeString };
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
