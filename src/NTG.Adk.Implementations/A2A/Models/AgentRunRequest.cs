// Copyright 2025 NTG
// Licensed under Apache License, Version 2.0

namespace NTG.Adk.Implementations.A2A.Models;

/// <summary>
/// Internal model for ADK agent run request.
/// Based on Google ADK Python implementation.
/// </summary>
public sealed record AgentRunRequest
{
    /// <summary>
    /// User ID for the request
    /// </summary>
    public required string UserId { get; init; }

    /// <summary>
    /// Session ID for the request
    /// </summary>
    public required string SessionId { get; init; }

    /// <summary>
    /// Invocation ID (optional)
    /// </summary>
    public string? InvocationId { get; init; }

    /// <summary>
    /// New message content from user
    /// </summary>
    public NTG.Adk.Boundary.Events.Content? NewMessage { get; init; }

    /// <summary>
    /// State delta to apply
    /// </summary>
    public IReadOnlyDictionary<string, object>? StateDelta { get; init; }

    /// <summary>
    /// Custom metadata for the run
    /// </summary>
    public IReadOnlyDictionary<string, object>? CustomMetadata { get; init; }
}
