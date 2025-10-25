// Copyright 2025 NTG
// Licensed under the Apache License, Version 2.0

using System.Collections.Concurrent;
using NTG.Adk.CoreAbstractions.Memory;
using NTG.Adk.CoreAbstractions.Sessions;

namespace NTG.Adk.Implementations.Sessions;

/// <summary>
/// In-memory implementation of ISession.
/// Adapter following A.D.D V3 pattern.
/// </summary>
public class InMemorySession : ISession
{
    public string AppName { get; init; } = "default";
    public string UserId { get; init; } = "default";
    public string SessionId { get; init; }
    public ISessionState State { get; init; }
    public IMessageHistory History { get; init; }
    public IList<CoreAbstractions.Events.IEvent> Events { get; init; }
    public DateTimeOffset LastUpdateTime { get; set; }
    public IMemoryService? Memory { get; init; }

    public InMemorySession(string? sessionId = null, IMemoryService? memoryService = null)
    {
        SessionId = sessionId ?? Guid.NewGuid().ToString();
        State = new InMemorySessionState();
        History = new InMemoryMessageHistory();
        Events = new List<CoreAbstractions.Events.IEvent>();
        LastUpdateTime = DateTimeOffset.UtcNow;
        Memory = memoryService;
    }

    public InMemorySession(
        string appName,
        string userId,
        string sessionId,
        ISessionState state,
        IMessageHistory? history = null,
        IList<CoreAbstractions.Events.IEvent>? events = null,
        DateTimeOffset? lastUpdateTime = null,
        IMemoryService? memory = null)
    {
        AppName = appName;
        UserId = userId;
        SessionId = sessionId;
        State = state;
        History = history ?? new InMemoryMessageHistory();
        Events = events ?? new List<CoreAbstractions.Events.IEvent>();
        LastUpdateTime = lastUpdateTime ?? DateTimeOffset.UtcNow;
        Memory = memory;
    }
}

/// <summary>
/// In-memory session state implementation
/// </summary>
public class InMemorySessionState : ISessionState
{
    private readonly ConcurrentDictionary<string, object> _state = new();

    public T? Get<T>(string key)
    {
        if (_state.TryGetValue(key, out var value) && value is T typedValue)
            return typedValue;
        return default;
    }

    public void Set<T>(string key, T value)
    {
        if (value is null)
        {
            _state.TryRemove(key, out _);
            return;
        }
        _state[key] = value;
    }

    public bool Contains(string key) => _state.ContainsKey(key);

    public bool TryGetValue<T>(string key, out T? value)
    {
        if (_state.TryGetValue(key, out var obj) && obj is T typedValue)
        {
            value = typedValue;
            return true;
        }
        value = default;
        return false;
    }

    public IEnumerable<string> Keys => _state.Keys;

    public void Clear() => _state.Clear();
}

/// <summary>
/// In-memory message history implementation
/// </summary>
public class InMemoryMessageHistory : IMessageHistory
{
    private readonly List<IMessage> _messages = new();
    private readonly object _lock = new();

    public void Add(IMessage message)
    {
        lock (_lock)
        {
            _messages.Add(message);
        }
    }

    public IReadOnlyList<IMessage> GetAll()
    {
        lock (_lock)
        {
            return _messages.ToList();
        }
    }

    public IReadOnlyList<IMessage> GetBranch(string branch)
    {
        lock (_lock)
        {
            return _messages.Where(m => m.Branch == branch).ToList();
        }
    }

    public void Clear()
    {
        lock (_lock)
        {
            _messages.Clear();
        }
    }
}

/// <summary>
/// Simple message implementation
/// </summary>
public record Message : IMessage
{
    public required string? Role { get; init; }
    public required string? Content { get; init; }
    public string? Branch { get; init; }
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
}
