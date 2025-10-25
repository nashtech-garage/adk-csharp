// Copyright 2025 NTG
// Licensed under the Apache License, Version 2.0

namespace NTG.Adk.Boundary.Events;

/// <summary>
/// Represents the response from a function call.
/// Equivalent to google.genai.types.FunctionResponse in Python.
/// </summary>
public record FunctionResponse
{
    /// <summary>
    /// Name of the function that was called
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// The response data from the function
    /// </summary>
    public required object Response { get; init; }

    /// <summary>
    /// Optional ID matching the FunctionCall.Id
    /// </summary>
    public string? Id { get; init; }

    /// <summary>
    /// Error information if the function call failed
    /// </summary>
    public string? Error { get; init; }
}
