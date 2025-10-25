// Copyright 2025 NTG
// Licensed under the Apache License, Version 2.0

namespace NTG.Adk.Boundary.Events;

/// <summary>
/// Content container holding multiple parts (text, function calls, etc.).
/// Equivalent to google.genai.types.Content in Python.
/// </summary>
public record Content
{
    /// <summary>
    /// The role of the content author (user, model, system, tool)
    /// </summary>
    public string? Role { get; init; }

    /// <summary>
    /// The parts that make up this content
    /// </summary>
    public required List<Part> Parts { get; init; }

    /// <summary>
    /// Create content with a single text part
    /// </summary>
    public static Content FromText(string text, string? role = null) => new()
    {
        Role = role,
        Parts = [Part.FromText(text)]
    };

    /// <summary>
    /// Create content with a function call
    /// </summary>
    public static Content FromFunctionCall(FunctionCall functionCall, string? role = null) => new()
    {
        Role = role,
        Parts = [Part.FromFunctionCall(functionCall)]
    };
}
