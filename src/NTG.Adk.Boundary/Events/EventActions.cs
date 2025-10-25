// Copyright 2025 NTG
// Licensed under the Apache License, Version 2.0

namespace NTG.Adk.Boundary.Events;

/// <summary>
/// Actions associated with an event.
/// Equivalent to google.adk.events.EventActions in Python.
/// </summary>
public record EventActions
{
    /// <summary>
    /// If true, signals the current workflow to stop/escalate to parent.
    /// Used in LoopAgent to break the loop.
    /// </summary>
    public bool Escalate { get; init; }

    /// <summary>
    /// Request transfer to another agent by name.
    /// Used in multi-agent delegation.
    /// </summary>
    public string? TransferTo { get; init; }

    /// <summary>
    /// State changes to apply to the session.
    /// Supports app-level (app:key), user-level (user:key), and session-level (key) state.
    /// </summary>
    public Dictionary<string, object>? StateDelta { get; init; }

    /// <summary>
    /// Additional custom actions
    /// </summary>
    public Dictionary<string, object>? CustomActions { get; init; }
}
