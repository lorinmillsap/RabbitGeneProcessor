namespace RabbitGeneProcessor.Core;

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
    List<string> AlternativeNotations);

/// <summary>
/// Represents the definition of a genetic locus.
/// </summary>
public record LocusDefinition(
    string Symbol,
    string Name,
    string Description,
    string Category,
    List<AlleleDefinition> Alleles);
