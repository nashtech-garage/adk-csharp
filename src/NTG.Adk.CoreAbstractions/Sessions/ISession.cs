// Copyright 2025 NTG
// Licensed under the Apache License, Version 2.0

using NTG.Adk.CoreAbstractions.Memory;

namespace NTG.Adk.CoreAbstractions.Sessions;

/// <summary>
/// Port interface for session management.
/// Equivalent to google.adk.sessions.Session in Python.
/// </summary>
public interface ISession
{
    /// <summary>
    /// Application name
    /// </summary>
    string AppName { get; }

    /// <summary>
    /// User ID
    /// </summary>
    string UserId { get; }

    /// <summary>
    /// Unique session identifier
    /// </summary>
    string SessionId { get; }

    /// <summary>
    /// Session state (key-value store for passing data between agents)
    /// </summary>
    ISessionState State { get; }

    /// <summary>
    /// Message history for this session
    /// </summary>
    IMessageHistory History { get; }

    /// <summary>
    /// Events for this session
    /// </summary>
    IList<Events.IEvent> Events { get; }

    /// <summary>
    /// Last update timestamp
    /// </summary>
    DateTimeOffset LastUpdateTime { get; set; }

    /// <summary>
    /// Long-term memory service (optional)
    /// </summary>
    IMemoryService? Memory { get; }
}

/// <summary>
/// Session state interface
/// </summary>
public interface ISessionState
{
    /// <summary>
    /// Get a value from state
    /// </summary>
    T? Get<T>(string key);

    /// <summary>
    /// Set a value in state
    /// </summary>
    void Set<T>(string key, T value);

    /// <summary>
    /// Check if key exists
    /// </summary>
    bool Contains(string key);

    /// <summary>
    /// Try to get a value
    /// </summary>
    bool TryGetValue<T>(string key, out T? value);

    /// <summary>
    /// Get all keys
    /// </summary>
    IEnumerable<string> Keys { get; }

    /// <summary>
    /// Clear all state
    /// </summary>
    void Clear();
}

/// <summary>
/// Message history interface
/// </summary>
public interface IMessageHistory
{
    /// <summary>
    /// Add a message to history
    /// </summary>
    void Add(IMessage message);

    /// <summary>
    /// Get all messages
    /// </summary>
    IReadOnlyList<IMessage> GetAll();

    /// <summary>
    /// Get messages for a specific branch
    /// </summary>
    IReadOnlyList<IMessage> GetBranch(string branch);

    /// <summary>
    /// Clear history
    /// </summary>
    void Clear();
}

/// <summary>
/// Message interface
/// </summary>
public interface IMessage
{
    string? Role { get; }
    string? Content { get; }
    string? Branch { get; }
    DateTimeOffset Timestamp { get; }
}
