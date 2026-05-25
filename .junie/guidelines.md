# AI Coding Assistant Guidelines

This document establishes the goals, scope, and general programming rules for AI coding assistants working on the RabbitGeneProcessor project.

## Goals
- Develop a robust and efficient processor for rabbit genetic data.
- Maintain high code quality and readability.
- Ensure type safety and modern C# practices.

## Scope
- Core logic for genetic sequence analysis.
- Data import/export for various genetic formats.
- Command-line interface for processing tasks.

## General Programming Rules
- **Language & Framework**: Use C# 14.0 and .NET 10.0.
- **Coding Style**: Follow standard C# naming conventions (PascalCase for classes/methods, camelCase for local variables).
- **Documentation**: Use XML documentation comments for public members.
- **Error Handling**: Use structured exception handling and provide meaningful error messages.
- **Testing**: Implement unit tests for core logic using a suitable testing framework (e.g., xUnit).
- **AI Interactions**:
    - AI should explain complex logic before implementation.
    - AI must adhere to these guidelines in every session.
    - AI should prioritize performance when dealing with large genetic datasets.
