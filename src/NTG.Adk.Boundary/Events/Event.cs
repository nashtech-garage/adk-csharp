// Copyright 2025 NTG
// Licensed under the Apache License, Version 2.0

namespace NTG.Adk.Boundary.Events;

/// <summary>
/// Represents an event in the agent system.
/// Equivalent to google.adk.events.Event in Python.
/// </summary>
public record Event
{
    /// <summary>
    /// The author/source of this event (agent name, tool name, etc.)
    /// </summary>
    public required string Author { get; init; }

    /// <summary>
    /// The content parts of this event (text, function calls, responses, etc.)
    /// </summary>
    public Content? Content { get; init; }

    /// <summary>
    /// Actions to take based on this event (escalate, etc.)
    /// </summary>
    public EventActions? Actions { get; init; }

    /// <summary>
    /// Additional metadata about this event
    /// </summary>
    public Dictionary<string, object>? Metadata { get; init; }

    /// <summary>
    /// Timestamp when this event was created
    /// </summary>
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Whether this is a partial event (streaming)
    /// </summary>
    public bool Partial { get; init; }

    /// <summary>
    /// Branch identifier for multi-agent context isolation
    /// (Python ADK compatibility)
    /// </summary>
    public string? Branch { get; init; }

    /// <summary>
    /// Invocation identifier to track which user turn this event belongs to
    /// (Python ADK compatibility - used for sliding window compaction)
    /// </summary>
    public string? InvocationId { get; init; }
}
