// Copyright 2025 NTG
// Licensed under the Apache License, Version 2.0

using NTG.Adk.CoreAbstractions.Tools;

namespace NTG.Adk.Implementations.Tools;

/// <summary>
/// Built-in tools for ADK.
/// Equivalent to google.adk.tools in Python.
/// </summary>
public static class BuiltInTools
{
    /// <summary>
    /// Transfer control to another agent.
    /// Equivalent to google.adk.tools.transfer_to_agent in Python.
    /// </summary>
    public static string TransferToAgent(string agent_name, IToolContext tool_context)
    {
        tool_context.Actions.TransferToAgent = agent_name;
        return $"Transferring to agent: {agent_name}";
    }

    /// <summary>
    /// Exit the current loop.
    /// Equivalent to google.adk.tools.exit_loop in Python.
    /// </summary>
    public static string ExitLoop(IToolContext tool_context)
    {
        tool_context.Actions.Escalate = true;
        return "Exiting loop";
    }

    /// <summary>
    /// Create the built-in transfer_to_agent tool.
    /// </summary>
    public static ITool CreateTransferToAgentTool()
    {
        return FunctionTool.Create<string, IToolContext, string>(
            TransferToAgent,
            "transfer_to_agent",
            "Transfer the question to another agent. Use this when you need to delegate to a specialized agent."
        );
    }

    /// <summary>
    /// Create the built-in exit_loop tool.
    /// </summary>
    public static ITool CreateExitLoopTool()
    {
        return FunctionTool.Create<IToolContext, string>(
            ExitLoop,
            "exit_loop",
            "Exit the current loop. Call this function only when you are instructed to do so."
        );
    }
}
