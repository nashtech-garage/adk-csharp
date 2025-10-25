// Copyright 2025 NTG
// Licensed under the Apache License, Version 2.0

using NTG.Adk.Boundary.Events;

namespace NTG.Adk.Boundary.Memory;

/// <summary>
/// Represents one memory entry from past conversations.
/// Equivalent to google.adk.memory.MemoryEntry in Python.
/// </summary>
public sealed record MemoryEntry
{
    /// <summary>
    /// The main content of the memory (conversation turn)
    /// </summary>
    public required Content Content { get; init; }

    /// <summary>
    /// The author of this memory (agent name, user, tool, etc.)
    /// </summary>
    public string? Author { get; init; }

    /// <summary>
    /// The timestamp when this memory was created (ISO 8601 format)
    /// </summary>
    public string? Timestamp { get; init; }
}

/// <summary>
/// Response from a memory search operation
/// </summary>
public sealed record SearchMemoryResponse
{
    /// <summary>
    /// List of memory entries that match the search query
    /// </summary>
    public required List<MemoryEntry> Memories { get; init; }
}
