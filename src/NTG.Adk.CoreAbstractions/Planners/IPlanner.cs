// Copyright 2025 NTG
// Licensed under the Apache License, Version 2.0

namespace NTG.Adk.CoreAbstractions.Planners;

/// <summary>
/// Port interface for agent planning capabilities.
/// Equivalent to google.adk.planners.BasePlanner in Python.
///
/// Planners allow agents to generate plans for queries to guide their actions.
/// This can include adding planning instructions to LLM requests or processing
/// responses to extract planning information.
/// </summary>
public interface IPlanner
{
    /// <summary>
    /// Builds planning instruction to be appended to the system instruction.
    /// Called before the LLM request is sent.
    /// </summary>
    /// <returns>Planning instruction to append, or null if no instruction needed</returns>
    string? BuildPlanningInstruction();

    /// <summary>
    /// Gets the planner type identifier for special handling.
    /// For example, "built-in" for native model thinking capabilities.
    /// </summary>
    string PlannerType { get; }
}
