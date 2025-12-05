// Copyright 2025 NTG
// Licensed under the Apache License, Version 2.0

namespace NTG.Adk.CoreAbstractions.Events;

/// <summary>
/// Port interface for events.
/// </summary>
public interface IEvent
{
    /// <summary>
    /// The author/source of this event
    /// </summary>
    string Author { get; }

    /// <summary>
    /// The content of this event
    /// </summary>
    IContent? Content { get; }

    /// <summary>
    /// Actions associated with this event
    /// </summary>
    IEventActions? Actions { get; }

    /// <summary>
    /// Metadata about the event
    /// </summary>
    IReadOnlyDictionary<string, object>? Metadata { get; }

    /// <summary>
    /// Timestamp of the event
    /// </summary>
    DateTimeOffset Timestamp { get; }

    /// <summary>
    /// Whether this is a partial event (streaming)
    /// </summary>
    bool Partial { get; }

    /// <summary>
    /// Branch identifier for multi-agent context isolation
    /// (Python ADK compatibility)
    /// </summary>
    string? Branch { get; }

    /// <summary>
    /// Invocation identifier to track which user turn this event belongs to
    /// (Python ADK compatibility - used for sliding window compaction)
    /// </summary>
    string? InvocationId { get; }
}

/// <summary>
/// Content interface
/// </summary>
public interface IContent
{
    string? Role { get; }
    IReadOnlyList<IPart> Parts { get; }
}

/// <summary>
/// Part interface
/// </summary>
public interface IPart
{
    string? Text { get; }
    string? Reasoning { get; }
    IFunctionCall? FunctionCall { get; }
    IFunctionResponse? FunctionResponse { get; }
    byte[]? InlineData { get; }
    string? MimeType { get; }
}

/// <summary>
/// Function call interface
/// </summary>
public interface IFunctionCall
{
    string Name { get; }
    IReadOnlyDictionary<string, object>? Args { get; }
    string? Id { get; }
}

/// <summary>
/// Function response interface
/// </summary>
public interface IFunctionResponse
{
    string Name { get; }
    object Response { get; }
    string? Id { get; }
    string? Error { get; }
}

/// <summary>
/// Event actions interface
/// </summary>
public interface IEventActions
{
    bool Escalate { get; }
    string? TransferTo { get; }
    IReadOnlyDictionary<string, object>? StateDelta { get; }
    IReadOnlyDictionary<string, object>? CustomActions { get; }
    IEventCompaction? Compaction { get; }
}

/// <summary>
/// Event compaction interface
/// </summary>
public interface IEventCompaction
{
    double StartTimestamp { get; }
    double EndTimestamp { get; }
    IContent CompactedContent { get; }
}
