// Copyright 2025 NTG
// Licensed under the Apache License, Version 2.0

using System.Text.Json;

namespace NTG.Adk.Boundary.Events;

/// <summary>
/// Represents a function call from the LLM.
/// Equivalent to google.genai.types.FunctionCall in Python.
/// </summary>
public record FunctionCall
{
    /// <summary>
    /// Name of the function to call
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Arguments to pass to the function
    /// </summary>
    public Dictionary<string, object>? Args { get; init; }

    /// <summary>
    /// Unique identifier for this function call (for tracking responses)
    /// </summary>
    public string? Id { get; init; }

    /// <summary>
    /// Get argument as strongly-typed value
    /// </summary>
    public T? GetArg<T>(string key)
    {
        if (Args == null || !Args.TryGetValue(key, out var value))
            return default;

        if (value is T typedValue)
            return typedValue;

        // Try JSON deserialization for complex types
        if (value is JsonElement jsonElement)
            return JsonSerializer.Deserialize<T>(jsonElement.GetRawText());

        return default;
    }
}
