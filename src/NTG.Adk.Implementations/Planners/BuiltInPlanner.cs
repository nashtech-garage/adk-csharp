// Copyright 2025 NTG
// Licensed under the Apache License, Version 2.0

using NTG.Adk.CoreAbstractions.Planners;

namespace NTG.Adk.Implementations.Planners;

/// <summary>
/// Built-in planner that uses the model's native thinking capabilities.
/// Equivalent to google.adk.planners.BuiltInPlanner in Python.
///
/// This planner relies on extended thinking features built into advanced models
/// like Gemini 2.0 Thinking. It does not add additional planning instructions,
/// instead signaling to the agent to enable native thinking mode.
///
/// Note: Thinking capabilities require model support. For Gemini 2.0+, this
/// enables extended reasoning mode. For other models, this may have no effect.
/// </summary>
public class BuiltInPlanner : IPlanner
{
    /// <summary>
    /// Creates a new BuiltInPlanner instance.
    /// </summary>
    public BuiltInPlanner()
    {
    }

    /// <summary>
    /// Built-in planner does not add planning instructions.
    /// Returns null to indicate no additional instruction is needed.
    /// </summary>
    public string? BuildPlanningInstruction()
    {
        return null;
    }

    /// <summary>
    /// Planner type identifier for special handling.
    /// </summary>
    public string PlannerType => "built-in";
}
