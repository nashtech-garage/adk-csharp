// Copyright 2025 NTG
// Licensed under the Apache License, Version 2.0

using NTG.Adk.CoreAbstractions.Sessions;
using NTG.Adk.CoreAbstractions.Tools;

namespace NTG.Adk.Implementations.Tools;

/// <summary>
/// Implementation of IToolContext.
/// Provides context for tool execution.
/// </summary>
public class ToolContext : IToolContext
{
    public ISessionState State { get; init; }
    public string? User { get; init; }
    public IReadOnlyDictionary<string, object>? Metadata { get; init; }
    public IToolActions Actions { get; init; }

    public ToolContext(ISessionState state, string? user = null, IReadOnlyDictionary<string, object>? metadata = null)
    {
        State = state;
        User = user;
        Metadata = metadata;
        Actions = new ToolActions();
    }
}

/// <summary>
/// Implementation of IToolActions.
/// Actions that tools can set during execution.
/// </summary>
public class ToolActions : IToolActions
{
    public string? TransferToAgent { get; set; }
    public bool Escalate { get; set; }
    public bool SkipSummarization { get; set; }
}
