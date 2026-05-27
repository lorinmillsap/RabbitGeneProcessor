namespace RabbitGeneProcessor.Core;

/// <summary>
/// Defines the placement of a genetic modifier relative to the variety name.
/// </summary>
public enum ModifierType
{
    Prefix,
    Suffix
}

/// <summary>
/// Represents a rabbit breed definition.
/// </summary>
public record BreedDefinition(
    string Name,
    string GenotypeString,
    string Description = "",
    List<string>? AlternateNames = null,
    ModifierType? Type = null,
    List<VarietyDefinition>? Varieties = null);

/// <summary>
/// Represents a rabbit variety definition based on its genotype.
/// </summary>
public record VarietyDefinition(
    string Name,
    string GenotypeString,
    string Description = "",
    string? ExclusionGenotypeString = null,
    List<string>? AlternateNames = null,
    ModifierType? Type = null);
