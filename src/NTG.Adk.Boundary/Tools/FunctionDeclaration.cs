// Copyright 2025 NTG
// Licensed under the Apache License, Version 2.0

namespace NTG.Adk.Boundary.Tools;

/// <summary>
/// Declaration of a function/tool that can be called by the LLM.
/// Equivalent to google.genai.types.FunctionDeclaration in Python.
/// </summary>
public record FunctionDeclaration
{
    /// <summary>
    /// Name of the function
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Description of what the function does
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// JSON schema describing the parameters
    /// </summary>
    public Schema? Parameters { get; init; }
}

/// <summary>
/// JSON schema for function parameters.
/// </summary>
public record Schema
{
    /// <summary>
    /// Type of the schema (e.g., "object")
    /// </summary>
    public string Type { get; init; } = "object";

    /// <summary>
    /// Properties of the schema
    /// </summary>
    public Dictionary<string, SchemaProperty>? Properties { get; init; }

    /// <summary>
    /// Required property names
    /// </summary>
    public List<string>? Required { get; init; }

    /// <summary>
    /// Description of the schema
    /// </summary>
    public string? Description { get; init; }
}

/// <summary>
/// A single property in a schema.
/// </summary>
public record SchemaProperty
{
    /// <summary>
    /// Type of the property (string, number, boolean, object, array)
    /// </summary>
    public required string Type { get; init; }

    /// <summary>
    /// Description of the property
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Enum values for string types
    /// </summary>
    public List<string>? Enum { get; init; }

    /// <summary>
    /// Items schema for array types
    /// </summary>
    public SchemaProperty? Items { get; init; }

    /// <summary>
    /// Properties for object types
    /// </summary>
    public Dictionary<string, SchemaProperty>? Properties { get; init; }
}
