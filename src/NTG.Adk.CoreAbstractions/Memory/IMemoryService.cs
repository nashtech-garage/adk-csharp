// Copyright 2025 NTG
// Licensed under the Apache License, Version 2.0

using NTG.Adk.CoreAbstractions.Sessions;

namespace NTG.Adk.CoreAbstractions.Memory;

/// <summary>
/// Service for managing searchable long-term agent memory.
/// Equivalent to google.adk.memory.BaseMemoryService in Python.
///
/// Memory service allows agents to:
/// 1. Ingest conversation sessions into memory
/// 2. Search past conversations by query
/// 3. Recall relevant context from previous interactions
///
/// This enables agents to "remember" information across sessions and
/// maintain long-term context about users and conversations.
/// </summary>
public interface IMemoryService
{
    /// <summary>
    /// Adds a session's conversation history to memory.
    /// A session may be added multiple times during its lifetime to keep memory up-to-date.
    /// </summary>
    /// <param name="session">The session to add to memory</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task AddSessionToMemoryAsync(
        ISession session,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Searches memory for relevant past conversations.
    /// Returns memory entries (conversation turns) that match the query.
    /// </summary>
    /// <param name="appName">Application name</param>
    /// <param name="userId">User identifier</param>
    /// <param name="query">Search query</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Search response containing matching memory entries</returns>
    Task<ISearchMemoryResponse> SearchMemoryAsync(
        string appName,
        string userId,
        string query,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// A memory entry representing a past conversation turn
/// </summary>
public interface IMemoryEntry
{
    /// <summary>
    /// The conversation content
    /// </summary>
    Events.IContent Content { get; }

    /// <summary>
    /// Who created this memory (agent, user, tool)
    /// </summary>
    string? Author { get; }

    /// <summary>
    /// When this memory was created (ISO 8601 format)
    /// </summary>
    string? Timestamp { get; }
}

/// <summary>
/// Response from a memory search operation
/// </summary>
public interface ISearchMemoryResponse
{
    /// <summary>
    /// List of matching memory entries
    /// </summary>
    IReadOnlyList<IMemoryEntry> Memories { get; }
}
