using System.Text.Json;

namespace RabbitGeneProcessor.Core;

/// <summary>
/// Provides methods for parsing genetic strings based on defined loci and alleles.
/// </summary>
public static class GeneticParser
{
    private static List<LocusDefinition>? _definitions;

    public static void Initialize(string jsonPath)
    {
        var json = File.ReadAllText(jsonPath);
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() },
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true
        };
        _definitions = JsonSerializer.Deserialize<List<LocusDefinition>>(json, options);
    }

    public static List<LocusDefinition> Definitions => _definitions ?? throw new InvalidOperationException("Parser not initialized.");

    /// <summary>
    /// Parses a single locus string (e.g., "Aat", "Enen", "A_").
    /// </summary>
    public static Locus ParseLocus(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            throw new ArgumentException("Input cannot be empty.", nameof(input));

        // Try to match against known multi-character alleles from definitions
        // We want to find the longest matching allele symbol at the start of the string
        
        var allAlleles = Definitions.SelectMany(l => l.Alleles)
            .SelectMany(a => new[] { a.Symbol }.Concat(a.AlternativeNotations))
            .OrderByDescending(s => s.Length)
            .ToList();

        // Also add the unknown marker
        allAlleles.Add("_");

        string? firstAllele = null;
        string? secondAllele = null;

        // Find first allele
        foreach (var symbol in allAlleles)
        {
            if (input.StartsWith(symbol))
            {
                firstAllele = symbol;
                // Map back to standard symbol if it was an alternative
                firstAllele = NormalizeAllele(firstAllele);
                break;
            }
        }

        if (firstAllele == null)
        {
             // Fallback to first char if no match found (might be a new or undefined allele)
             firstAllele = input[0].ToString();
        }

        string remaining = input.Substring(MatchLength(input, firstAllele));

        if (string.IsNullOrEmpty(remaining))
        {
            secondAllele = "_";
        }
        else
        {
            foreach (var symbol in allAlleles)
            {
                if (remaining.StartsWith(symbol))
                {
                    secondAllele = symbol;
                    secondAllele = NormalizeAllele(secondAllele);
                    break;
                }
            }
            
            if (secondAllele == null)
            {
                secondAllele = remaining; // Take whatever is left
            }
        }

        return new Locus(new Allele(firstAllele), new Allele(secondAllele));
    }

    private static string NormalizeAllele(string symbol)
    {
        if (symbol == "_") return "_";
        foreach (var locus in Definitions)
        {
            foreach (var allele in locus.Alleles)
            {
                if (allele.Symbol == symbol || allele.AlternativeNotations.Contains(symbol))
                {
                    return allele.Symbol;
                }
            }
        }
        return symbol;
    }

    private static int MatchLength(string input, string normalizedSymbol)
    {
        // Find what actually matched in the input to know how much to skip
        if (input.StartsWith(normalizedSymbol)) return normalizedSymbol.Length;
        
        foreach (var locus in Definitions)
        {
            foreach (var allele in locus.Alleles)
            {
                if (allele.Symbol == normalizedSymbol)
                {
                    foreach (var alt in allele.AlternativeNotations)
                    {
                        if (input.StartsWith(alt)) return alt.Length;
                    }
                }
            }
        }
        return normalizedSymbol.Length;
    }
}
