# RabbitGeneProcessor

RabbitGeneProcessor is a robust and efficient tool for processing rabbit genetic data. It aims to handle known rabbit genetics and provide various ways to process and analyze them.

## High-Level Objectives

- **Genetic Sequence Analysis**: Process and analyze rabbit genetic sequences based on established allele rules.
- **Variety Identification**: Apply a set of known genetics against a database of varieties to identify rabbit types.
- **Genetic Modeling**: Support unknown or masked alleles in genetic strings.
- **Inheritance Calculation**: Implement Punnett square logic to predict offspring genotypes based on parental genetics.
- **Performance**: Prioritize efficient processing for large genetic datasets.

## Genetic Representation

Genes are represented as allele pairs, with each locus separated by a comma. 

### Rules:
- **Case Sensitivity**: Case sensitivity is critical (e.g., `En` vs `en`).
- **Separators**: Each locus is separated by a comma (`,`).
- **Unknowns**: An underscore (`_`) indicates an unknown or masked allele.
- **Allele Pairs**: Alleles are typically shown in pairs or as a single dominant allele with a mask.

### Inheritance (Punnett Squares)
Genetics work on a Punnett square principle:
- Each locus consists of 2 alleles.
- Each parent passes on exactly one copy of one of its alleles to its offspring.
- There is approximately a 50% chance for either allele at a locus to be passed down.

### Example: `A_,B_,C_,D_,E,enen`
- `A_`: Agouti (dominant `A`) with an unknown recessive.
- `B_`: Black (dominant `B`) with an unknown recessive.
- `C_`: Full Color (dominant `C`) with an unknown recessive.
- `D_`: Dense (dominant `D`) with an unknown recessive.
- `E_`: Full Extension (dominant `E`) with an unknown recessive.
- `enen`: Non-broken (recessive `en`, two copies required for the phenotype).
- **Result**: This specific combination describes a standard **Chestnut** rabbit.

## Project Structure
- **Core Logic**: Genetic sequence analysis and processing.
- **Data**: Support for variety databases and genetic formats.
- **CLI**: Command-line interface for interaction and processing tasks.

## Guidelines
Project-specific AI coding guidelines can be found in `.junie/guidelines.md`.
