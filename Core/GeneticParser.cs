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
    /// Gets the locus symbol (e.g., "A", "C") for a given allele symbol (e.g., "at", "cchd").
    /// </summary>
    public static string GetLocusSymbol(string alleleSymbol)
    {
        if (alleleSymbol == "_") return "Unknown";
        if (_definitions == null) return "Unknown";
        foreach (var locus in _definitions)
        {
            if (locus.Alleles.Any(a => a.Symbol == alleleSymbol))
            {
                return locus.Symbol;
            }
        }
        return "Unknown";
    }

    /// <summary>
    /// Parses a single locus string (e.g., "Aat", "Enen", "A_", "__", "A(ata)").
    /// </summary>
    public static Locus ParseLocus(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            throw new ArgumentException("Input cannot be empty.", nameof(input));

        var alleles = new List<Allele>();
        int index = 0;

        while (index < input.Length && alleles.Count < 2)
        {
            var alleleResult = ParseNextAllele(input, ref index);
            alleles.Add(alleleResult);
        }

        if (alleles.Count == 1)
        {
            var first = alleles[0];
            // If the first allele has suspected/excluded but is not unknown, 
            // it's likely a shorthand for the recessive position: A(ata) -> A, _(ata)
            if (!first.IsUnknown && (first.Suspected is { Count: > 0 } || first.Excluded is { Count: > 0 }))
            {
                alleles[0] = first with { Suspected = null, Excluded = null };
                alleles.Add(new Allele("_", first.Suspected, first.Excluded));
            }
            else
            {
                alleles.Add(new Allele("_"));
            }
        }
        
        // Ensure we always have 2 alleles
        while (alleles.Count < 2)
        {
            alleles.Add(new Allele("_"));
        }

        return new Locus(alleles[0], alleles[1]);
    }

    private static Allele ParseNextAllele(string input, ref int index)
    {
        var allSymbols = Definitions.SelectMany(l => l.Alleles)
            .SelectMany(a => new[] { a.Symbol }.Concat(a.AlternativeNotations))
            .OrderByDescending(s => s.Length)
            .ToList();
        allSymbols.Add("_");
        allSymbols.Add("*");
        allSymbols.Add("?");

        string? baseSymbol = null;
        foreach (var symbol in allSymbols)
        {
            var span = input.AsSpan(index);
            if (span.StartsWith(symbol))
            {
                baseSymbol = symbol;
                index += symbol.Length;
                break;
            }

            // Check if symbol contains '^' and input does not, or vice versa
            var normalizedSymbol = symbol.Replace("^", "");
            if (normalizedSymbol != symbol)
            {
                if (span.StartsWith(normalizedSymbol))
                {
                    // Check if we didn't just match a shorter real symbol
                    // AllSymbols is sorted by descending length, so this logic is slightly tricky.
                    // But if symbol was "a^t" and normalized is "at", and we are at "at", 
                    // we should probably accept it as a match for the same allele.
                    baseSymbol = symbol;
                    index += normalizedSymbol.Length;
                    break;
                }
            }
        }

        if (baseSymbol == null)
        {
            baseSymbol = input[index].ToString();
            index++;
        }

        baseSymbol = NormalizeAllele(baseSymbol);

        List<string>? suspected = null;
        List<string>? excluded = null;
        bool useSlashInSuspected = false;

        while (index < input.Length)
        {
            if (input[index] == '(')
            {
                index++;
                suspected = ParseAlleleList(input, ref index, ')', out useSlashInSuspected);
            }
            else if (input[index] == '[')
            {
                index++;
                excluded = ParseAlleleList(input, ref index, ']', out _);
            }
            else
            {
                break;
            }
        }

        return new Allele(baseSymbol, suspected, excluded, useSlashInSuspected);
    }

    private static List<string> ParseAlleleList(string input, ref int index, char endChar, out bool detectedSlash)
    {
        detectedSlash = false;
        var list = new List<string>();
        var allSymbols = Definitions.SelectMany(l => l.Alleles)
            .SelectMany(a => new[] { a.Symbol }.Concat(a.AlternativeNotations))
            .OrderByDescending(s => s.Length)
            .ToList();

        while (index < input.Length && input[index] != endChar)
        {
            if (input[index] == '/')
            {
                index++;
                detectedSlash = true;
                continue; // Skip the slash separator
            }

            string? foundSymbol = null;
            foreach (var symbol in allSymbols)
            {
                if (input.AsSpan(index).StartsWith(symbol))
                {
                    foundSymbol = symbol;
                    index += symbol.Length;
                    break;
                }
            }

            if (foundSymbol != null)
            {
                list.Add(NormalizeAllele(foundSymbol));
            }
            else
            {
                list.Add(input[index].ToString());
                index++;
            }
        }

        if (index < input.Length && input[index] == endChar)
        {
            index++;
        }

        return list;
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
}
