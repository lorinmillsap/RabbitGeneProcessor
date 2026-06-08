# Rabbit Genetic Prediction Rules

This document outlines the rules and order of operations used by the RabbitGeneProcessor for predicting offspring genotypes and phenotypes.

## 1. Syntax & Allele Support
- **Standard Alleles**: Follows standard rabbit genetic nomenclature (e.g., `A`, `at`, `a`, `B`, `b`).
- **Suspected Alleles**: Supports suspected alleles using brackets (e.g., `A[a]`).
- **Excluded Alleles**: Supports excluded alleles using curly braces (e.g., `A{!at}`).
- **Unknown Alleles (`_`)**: A wildcard `_` represents an unknown allele that is equal in dominance or recessive to the known allele at that locus.

## 2. Breed Influence
- **Locus Inclusion**: A breed may bring specific loci into play that are not explicitly mentioned in the genotype. These loci are added to the parents before calculation.
- **Breed Inheritance**:
    - If both parents are of the same breed, the offspring is assigned that breed.
    - If parents are of different breeds, the offspring is marked as a "Mixed Breed".
    - If no breed is mentioned, it is assumed to be the same as the input.

## 3. Order of Operations
1. **Normalization**: Parse parent genotypes and apply breed-specific defaults or required loci.
2. **Expansion**: Expand wildcards (`_`) and suspected alleles into all biologically possible alleles based on dominance rules and the `LociDefinitions.json`.
3. **Punnett Square Calculation**: Generate all possible gamete combinations from both parents and combine them to form offspring genotypes.
4. **Probability Calculation**: Determine the frequency of each unique genotype outcome.
5. **Trait Categorization**:
    - **Color/Variety**: Consolidated based on the variety description mask.
    - **Body, Fur, and Pattern**: Calculated independently using Mendelian inheritance. These categories always sum to 100%.
6. **Consolidation**: Group identical phenotypic outcomes and sum their probabilities.
7. **Limiting**: Limit the variety list to the requested number of top results.

## 4. Special Locus Handling
- **Dwarf Locus (`Dw`)**:
    - `DwDw`: Peanut (Lethal) - 25% chance if both parents are `Dwdw`.
    - `Dwdw`: Dwarf.
    - `dwdw`: Normal Size.
    - Unlike other loci, `Dw` is never simplified to `Dw_` to ensure lethal states are correctly tracked.

## 5. Variety Identification
- Outcomes are matched against known variety definitions.
- If a genotype doesn't perfectly match a known variety, the system uses the closest approximation based on available genetic data.
