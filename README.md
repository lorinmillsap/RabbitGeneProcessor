# RabbitGeneProcessor

RabbitGeneProcessor is a robust and efficient tool for processing rabbit genetic data. It aims to handle known rabbit genetics, calculate genotypes from descriptions, identify varieties from genotypes, and solve for hidden recessive alleles using parental or offspring evidence.

## High-Level Objectives

- **Genetic Sequence Analysis**: Process and analyze rabbit genetic sequences based on established allele rules.
- **Variety Identification**: Map genetic strings back to variety names (e.g., "Chestnut", "Broken VM Lilac Rex") using a database-driven identification engine.
- **Genotype Calculation**: Convert descriptive names into full genotypes, applying breed-specific traits, variety rules, and multiple modifiers.
- **Genetic Modeling**: Support complex notations including unknowns (`_`), suspected alleles `()`, excluded alleles `[]`, and wildcards (`*`).
- **Inheritance & Solving**: 
    - **Punnett Squares**: Predict offspring genotypes and variety distributions based on parental genetics.
    - **Offspring Solver**: Deduce unknown recessive alleles in an offspring using its parents' genotypes.
    - **Parent Solver**: Identify carrier alleles in parents based on evidence from their offspring.
- **CLI Interface**: A comprehensive command-line tool for interacting with the genetic engine.

## Genetic Representation

Genes are represented as allele pairs, with each locus separated by a comma (`,`) or a space (` `).

### Dominance Rules:
- **Universal Logic**: All loci follow the same dominance rules. We don't need special code for each locus.
- **Dominance Hierarchy**: Alleles are ordered by dominance: Dominant > Partially Dominant > Partially Recessive > Recessive.
- **Dominant Genes**: Always dominant. What the recessive allele is doesn't matter once the most dominant gene is established (e.g., `A_` is phenotypically equivalent to `AA`, `Aa`, or `Aat`).
- **Stacking Genes**: Partially dominant genes (e.g., `at`, `cchd`) dominate based on their order of dominance, but their expression can still be modified by the recessive allele (this is referred to as "stacking").
- **Fully Recessive Genes**: Only express when no dominant alleles are present, always reflected as homozygous pairs (e.g., `aa`, `bb`, `cc`).
- **Partially Recessive Genes**: Can sometimes express over a dominant gene, but not in a predictable way.
- **Notation**: Our prediction respects syntax rules for suspects `()` and exclusions `[]`. When a gene combination calculates as a variation of a singular variety, we consolidate, displaying the dominant to unknown gene pairing (e.g., `A_`).

### Syntax Rules:
- **Case Sensitivity**: Case sensitivity is critical (e.g., `En` vs `en`).
- **Separators**: Each locus is separated by a comma (`,`) or a space (` `).
- **Optional Characters**: The caret (`^`) symbol is optional in genetic notations (e.g., `a^t` is equivalent to `at`, `c^chd` is equivalent to `cchd`).
- **Unknowns**: An underscore (`_`) indicates an unknown or masked allele. `__` indicates a completely unknown locus.
- **Suspects**: Alleles in parentheses `()` are suspected or likely possibilities (e.g., `A(at)`). Slashes can be used for "or" conditions (e.g., `cchl(ch/c)`).
- **Exclusions**: Alleles in square brackets `[]` are known exclusions (e.g., `A_[at]`).
- **Wildcards**: `*` or `?` preserve existing alleles during masking/overrides (e.g., `*ej` preserves the first allele and sets the second to `ej`).
- **Explicit Locus**: `{L}alleles` explicitly assigns alleles to a specific locus (e.g., `{A}__`).

## CLI Usage

The tool provides several commands to interact with the genetic engine.

### Calculate Genotype
Convert a variety description into a genetic string.
```bash
dotnet run -- calculate "Broken VM Chestnut Rex"
```

### Identify Variety
Identify the variety name from a genotype string.
```bash
dotnet run -- identify "aa,bb,C_,dd,E_,En_,Vv,rr" --breed Rex
```

### Solve Offspring
Deduce unknown alleles in a target offspring using its parents.
```bash
dotnet run -- solve-offspring --target "A_,B_,C_,D_,E_" --p1 "aa,B_,C_,D_,E_" --p2 "A_,B_,C_,D_,E_"
```

### Solve Parents
Deduce carrier alleles in parents using evidence from their offspring.
```bash
dotnet run -- solve-parents --p1 "A_,B_,C_,D_,E_" --p2 "A_,B_,C_,D_,E_" --offspring "aa,B_,C_,D_,E_" "A_,B_,C_,dd,E_"
```

### Predict Offspring
Predict possible genotypes and variety descriptions for offspring from two parents.
```bash
dotnet run -- predict --p1 "Aa,Bb,C_,D_,E_" --p2 "Aa,Bb,C_,D_,E_" --limit 10
```

## Project Structure

- **Core Logic**:
    - `GeneticParser`: Robust parsing of complex genetic strings.
    - `VarietyService`: Manages Breeds, Varieties, and Modifiers.
    - `GenotypeSolver`: Logic for resolving hidden alleles through inheritance.
- **Data**: JSON-based databases for:
    - `LociDefinitions.json`: Alleles, dominance types, and categories.
    - `Breeds.json`: Breed-specific genetic traits and varieties.
    - `Varieties.json`: Base variety genetic signatures.
    - `Modifiers.json`: Prefix and Suffix modifiers (e.g., Broken, VM, Tri).

## Guidelines
Project-specific AI coding guidelines can be found in `.junie/guidelines.md`.
