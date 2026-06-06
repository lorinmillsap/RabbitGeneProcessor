namespace RabbitGeneProcessor.Core;

/// <summary>
/// Genetic dominance rules:
/// - All loci work the same based on established dominance types and order.
/// - Dominance hierarchy: Dominant > Partially Dominant > Partially Recessive > Recessive.
/// - Dominant genes always mask the recessive; the specific recessive doesn't matter (e.g., AA is phenotypically same as Aa).
/// - Stacking genes (partially dominant/recessive) dominate based on their order, but their expression can be modified by the recessive allele.
/// - Fully recessive genes only express when homozygous (e.g., aa).
/// - Partially recessive genes can sometimes express over a dominant gene, but not in a predictable way.
/// </summary>
public static class GeneticRules
{
}

/// <summary>
/// Defines the type of dominance an allele exhibits.
/// </summary>
public enum DominanceType
{
    Dominant,
    PartiallyDominant,
    Recessive,
    PartiallyRecessive
}

/// <summary>
/// Represents the definition of a single allele within a locus.
/// </summary>
public record AlleleDefinition(
    string Symbol,
    string Name,
    string Description,
    DominanceType Dominance,
    int Order,
    List<string> AlternativeNotations,
    bool IsDefault = false);

/// <summary>
/// Represents the definition of a genetic locus.
/// </summary>
public record LocusDefinition(
    string Symbol,
    string Name,
    string Description,
    string Category,
    List<AlleleDefinition> Alleles)
{
    public AlleleDefinition? DefaultAllele => Alleles.FirstOrDefault(a => a.IsDefault);
}
