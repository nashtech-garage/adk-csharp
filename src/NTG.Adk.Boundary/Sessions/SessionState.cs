// Copyright 2025 NTG
// Licensed under the Apache License, Version 2.0

namespace NTG.Adk.Boundary.Sessions;

/// <summary>
/// DTO representing session state.
/// Used to serialize/deserialize state across boundaries.
/// </summary>
public record SessionStateDto
{
    /// <summary>
    /// The state key-value pairs
    /// </summary>
    public required Dictionary<string, object> Data { get; init; }

    /// <summary>
    /// Session ID
    /// </summary>
    public required string SessionId { get; init; }

    /// <summary>
    /// Last modified timestamp
    /// </summary>
    public DateTimeOffset LastModified { get; init; } = DateTimeOffset.UtcNow;
}
