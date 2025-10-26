// Copyright 2025 NTG
// Licensed under the Apache License, Version 2.0

namespace NTG.Adk.CoreAbstractions.Agents;

/// <summary>
/// Configuration for agent execution.
/// Equivalent to google.adk.agents.RunConfig in Python.
/// </summary>
public class RunConfig
{
    /// <summary>
    /// Max number of LLM calls (including tool execution rounds) for a single invocation.
    /// Default: 500 (matches Python ADK)
    ///
    /// Valid Values:
    ///   - Greater than 0: Enforces the limit
    ///   - Less than or equal to 0: Allows unbounded LLM calls
    /// </summary>
    public int MaxLlmCalls { get; set; } = 500;
}
