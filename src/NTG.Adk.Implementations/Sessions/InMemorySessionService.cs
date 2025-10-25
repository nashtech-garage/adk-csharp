// Copyright 2025 NTG
// Licensed under the Apache License, Version 2.0

using System.Collections.Concurrent;
using NTG.Adk.CoreAbstractions.Events;
using NTG.Adk.CoreAbstractions.Sessions;

namespace NTG.Adk.Implementations.Sessions;

/// <summary>
/// In-memory implementation of ISessionService.
/// Equivalent to google.adk.sessions.InMemorySessionService in Python.
///
/// Note: Not suitable for multi-threaded production environments.
/// Use for testing and development only.
/// </summary>
public class InMemorySessionService : ISessionService
{
    // State prefixes matching Python ADK
    private const string APP_PREFIX = "app:";
    private const string USER_PREFIX = "user:";
    private const string TEMP_PREFIX = "temp:";

    // Three-level storage: app_name -> user_id -> session_id -> Session
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, ConcurrentDictionary<string, ISession>>> _sessions = new();

    // App-level state: app_name -> state
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, object>> _appState = new();

    // User-level state: app_name -> user_id -> state
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, ConcurrentDictionary<string, object>>> _userState = new();

    public async Task<ISession> CreateSessionAsync(
        string appName,
        string userId,
        IReadOnlyDictionary<string, object>? state = null,
        string? sessionId = null,
        CancellationToken cancellationToken = default)
    {
        // Generate session ID if not provided
        sessionId = string.IsNullOrWhiteSpace(sessionId)
            ? Guid.NewGuid().ToString()
            : sessionId.Trim();

        // Check if session already exists
        if (await GetSessionAsync(appName, userId, sessionId, cancellationToken: cancellationToken) != null)
        {
            throw new InvalidOperationException($"Session with id '{sessionId}' already exists.");
        }

        // Extract state deltas by prefix
        var stateDeltas = ExtractStateDeltas(state);

        // Update app-level state
        if (stateDeltas.AppState.Count > 0)
        {
            var appStateDict = _appState.GetOrAdd(appName, _ => new ConcurrentDictionary<string, object>());
            foreach (var kvp in stateDeltas.AppState)
            {
                appStateDict[kvp.Key] = kvp.Value;
            }
        }

        // Update user-level state
        if (stateDeltas.UserState.Count > 0)
        {
            var appUserState = _userState.GetOrAdd(appName, _ => new ConcurrentDictionary<string, ConcurrentDictionary<string, object>>());
            var userStateDict = appUserState.GetOrAdd(userId, _ => new ConcurrentDictionary<string, object>());
            foreach (var kvp in stateDeltas.UserState)
            {
                userStateDict[kvp.Key] = kvp.Value;
            }
        }

        // Create session with session-level state only
        var sessionState = new InMemorySessionState();
        foreach (var kvp in stateDeltas.SessionState)
        {
            sessionState.Set(kvp.Key, kvp.Value);
        }

        var session = new InMemorySession(
            appName: appName,
            userId: userId,
            sessionId: sessionId,
            state: sessionState,
            lastUpdateTime: DateTimeOffset.UtcNow
        );

        // Store session
        var appSessions = _sessions.GetOrAdd(appName, _ => new ConcurrentDictionary<string, ConcurrentDictionary<string, ISession>>());
        var userSessions = appSessions.GetOrAdd(userId, _ => new ConcurrentDictionary<string, ISession>());
        userSessions[sessionId] = session;

        // Return copy with merged state
        return await Task.FromResult(MergeState(appName, userId, DeepCopy(session)));
    }

    public async Task<ISession?> GetSessionAsync(
        string appName,
        string userId,
        string sessionId,
        GetSessionConfig? config = null,
        CancellationToken cancellationToken = default)
    {
        if (!_sessions.TryGetValue(appName, out var appSessions))
            return null;

        if (!appSessions.TryGetValue(userId, out var userSessions))
            return null;

        if (!userSessions.TryGetValue(sessionId, out var session))
            return null;

        // Deep copy to avoid external modification
        var copiedSession = DeepCopy(session);

        // Apply event filtering if config provided
        if (config != null)
        {
            var events = copiedSession.Events.ToList();

            // Filter by count
            if (config.NumRecentEvents.HasValue)
            {
                var count = config.NumRecentEvents.Value;
                events = events.Skip(Math.Max(0, events.Count - count)).ToList();
            }

            // Filter by timestamp
            if (config.AfterTimestamp.HasValue)
            {
                events = events.Where(e => e.Timestamp >= config.AfterTimestamp.Value).ToList();
            }

            copiedSession.Events.Clear();
            foreach (var evt in events)
            {
                copiedSession.Events.Add(evt);
            }
        }

        // Return with merged app/user state
        return await Task.FromResult(MergeState(appName, userId, copiedSession));
    }

    public async Task<IReadOnlyList<ISession>> ListSessionsAsync(
        string appName,
        string? userId = null,
        CancellationToken cancellationToken = default)
    {
        var result = new List<ISession>();

        if (!_sessions.TryGetValue(appName, out var appSessions))
            return result;

        if (userId != null)
        {
            // List for specific user
            if (appSessions.TryGetValue(userId, out var userSessions))
            {
                foreach (var session in userSessions.Values)
                {
                    var copied = DeepCopy(session);
                    copied.Events.Clear(); // Don't include events in list
                    result.Add(MergeState(appName, userId, copied));
                }
            }
        }
        else
        {
            // List for all users
            foreach (var userSessionsKvp in appSessions)
            {
                var currentUserId = userSessionsKvp.Key;
                foreach (var session in userSessionsKvp.Value.Values)
                {
                    var copied = DeepCopy(session);
                    copied.Events.Clear(); // Don't include events in list
                    result.Add(MergeState(appName, currentUserId, copied));
                }
            }
        }

        return await Task.FromResult(result);
    }

    public async Task DeleteSessionAsync(
        string appName,
        string userId,
        string sessionId,
        CancellationToken cancellationToken = default)
    {
        if (_sessions.TryGetValue(appName, out var appSessions))
        {
            if (appSessions.TryGetValue(userId, out var userSessions))
            {
                userSessions.TryRemove(sessionId, out _);
            }
        }

        await Task.CompletedTask;
    }

    public async Task<IEvent> AppendEventAsync(
        ISession session,
        IEvent @event,
        CancellationToken cancellationToken = default)
    {
        // Skip partial events
        if (@event.Partial)
            return @event;

        var appName = session.AppName;
        var userId = session.UserId;
        var sessionId = session.SessionId;

        // Find storage session
        if (!_sessions.TryGetValue(appName, out var appSessions) ||
            !appSessions.TryGetValue(userId, out var userSessions) ||
            !userSessions.TryGetValue(sessionId, out var storageSession))
        {
            return @event; // Session not found, return event unchanged
        }

        // Update session in parameter (for caller)
        session.Events.Add(@event);
        session.LastUpdateTime = @event.Timestamp;

        // Update storage session
        storageSession.Events.Add(@event);
        storageSession.LastUpdateTime = @event.Timestamp;

        // Process state delta from event actions
        if (@event.Actions?.StateDelta != null && @event.Actions.StateDelta.Count > 0)
        {
            var stateDeltas = ExtractStateDeltas(@event.Actions.StateDelta);

            // Update app-level state
            if (stateDeltas.AppState.Count > 0)
            {
                var appStateDict = _appState.GetOrAdd(appName, _ => new ConcurrentDictionary<string, object>());
                foreach (var kvp in stateDeltas.AppState)
                {
                    appStateDict[kvp.Key] = kvp.Value;
                }
            }

            // Update user-level state
            if (stateDeltas.UserState.Count > 0)
            {
                var appUserState = _userState.GetOrAdd(appName, _ => new ConcurrentDictionary<string, ConcurrentDictionary<string, object>>());
                var userStateDict = appUserState.GetOrAdd(userId, _ => new ConcurrentDictionary<string, object>());
                foreach (var kvp in stateDeltas.UserState)
                {
                    userStateDict[kvp.Key] = kvp.Value;
                }
            }

            // Update session-level state
            if (stateDeltas.SessionState.Count > 0)
            {
                foreach (var kvp in stateDeltas.SessionState)
                {
                    storageSession.State.Set(kvp.Key, kvp.Value);
                    session.State.Set(kvp.Key, kvp.Value);
                }
            }
        }

        return await Task.FromResult(@event);
    }

    // Helper: Extract state deltas by prefix
    private StateDeltas ExtractStateDeltas(IReadOnlyDictionary<string, object>? state)
    {
        var result = new StateDeltas();

        if (state == null)
            return result;

        foreach (var kvp in state)
        {
            if (kvp.Key.StartsWith(TEMP_PREFIX))
            {
                // Skip temporary keys
                continue;
            }
            else if (kvp.Key.StartsWith(APP_PREFIX))
            {
                // App-level state
                var key = kvp.Key.Substring(APP_PREFIX.Length);
                result.AppState[key] = kvp.Value;
            }
            else if (kvp.Key.StartsWith(USER_PREFIX))
            {
                // User-level state
                var key = kvp.Key.Substring(USER_PREFIX.Length);
                result.UserState[key] = kvp.Value;
            }
            else
            {
                // Session-level state (no prefix)
                result.SessionState[kvp.Key] = kvp.Value;
            }
        }

        return result;
    }

    // Helper: Merge app and user state into session copy
    private ISession MergeState(string appName, string userId, ISession session)
    {
        // Merge app-level state
        if (_appState.TryGetValue(appName, out var appStateDict))
        {
            foreach (var kvp in appStateDict)
            {
                session.State.Set(APP_PREFIX + kvp.Key, kvp.Value);
            }
        }

        // Merge user-level state
        if (_userState.TryGetValue(appName, out var appUserState) &&
            appUserState.TryGetValue(userId, out var userStateDict))
        {
            foreach (var kvp in userStateDict)
            {
                session.State.Set(USER_PREFIX + kvp.Key, kvp.Value);
            }
        }

        return session;
    }

    // Helper: Deep copy session
    private InMemorySession DeepCopy(ISession session)
    {
        var newState = new InMemorySessionState();
        foreach (var key in session.State.Keys)
        {
            if (session.State.TryGetValue<object>(key, out var value))
            {
                newState.Set(key, value);
            }
        }

        var newEvents = session.Events.ToList();

        return new InMemorySession(
            appName: session.AppName,
            userId: session.UserId,
            sessionId: session.SessionId,
            state: newState,
            history: session.History,
            events: newEvents,
            lastUpdateTime: session.LastUpdateTime,
            memory: session.Memory
        );
    }

    private class StateDeltas
    {
        public Dictionary<string, object> AppState { get; } = new();
        public Dictionary<string, object> UserState { get; } = new();
        public Dictionary<string, object> SessionState { get; } = new();
    }
}
