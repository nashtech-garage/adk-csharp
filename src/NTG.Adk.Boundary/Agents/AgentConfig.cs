// Copyright 2025 NTG
// Licensed under the Apache License, Version 2.0

namespace NTG.Adk.Boundary.Agents;

/// <summary>
/// Configuration for an agent.
/// Equivalent to google.adk.agents.AgentConfig in Python.
/// </summary>
public record AgentConfig
{
    /// <summary>
    /// Unique name of the agent
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Description of what this agent does
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// System instruction for the agent
    /// </summary>
    public string? Instruction { get; init; }

    /// <summary>
    /// Model to use (e.g., "gemini-2.5-flash")
    /// </summary>
    public string? Model { get; init; }

    /// <summary>
    /// State key to save agent output to
    /// </summary>
    public string? OutputKey { get; init; }

    /// <summary>
    /// Input schema for structured input
    /// </summary>
    public object? InputSchema { get; init; }

    /// <summary>
    /// Output schema for structured output
    /// </summary>
    public object? OutputSchema { get; init; }

    /// <summary>
    /// Maximum iterations for loop agents
    /// </summary>
    public int? MaxIterations { get; init; }

    /// <summary>
    /// Additional configuration properties
    /// </summary>
    public Dictionary<string, object>? AdditionalConfig { get; init; }
}
