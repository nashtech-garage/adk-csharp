// Copyright 2025 NTG
// Licensed under the Apache License, Version 2.0

using NTG.Adk.CoreAbstractions.Events;

namespace NTG.Adk.CoreAbstractions.Sessions;

/// <summary>
/// Port interface for session service.
/// Equivalent to google.adk.sessions.BaseSessionService in Python.
/// </summary>
public interface ISessionService
{
    /// <summary>
    /// Creates a new session.
    /// </summary>
    /// <param name="appName">The name of the app</param>
    /// <param name="userId">The ID of the user</param>
    /// <param name="state">Initial state (optional)</param>
    /// <param name="sessionId">Client-provided session ID (optional, auto-generated if not provided)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The newly created session</returns>
    Task<ISession> CreateSessionAsync(
        string appName,
        string userId,
        IReadOnlyDictionary<string, object>? state = null,
        string? sessionId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets an existing session.
    /// </summary>
    /// <param name="appName">The name of the app</param>
    /// <param name="userId">The ID of the user</param>
    /// <param name="sessionId">The session ID</param>
    /// <param name="config">Configuration for filtering events (optional)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The session, or null if not found</returns>
    Task<ISession?> GetSessionAsync(
        string appName,
        string userId,
        string sessionId,
        GetSessionConfig? config = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists all sessions for a user or all users.
    /// </summary>
    /// <param name="appName">The name of the app</param>
    /// <param name="userId">The ID of the user (optional, lists all users if not provided)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of sessions (without events)</returns>
    Task<IReadOnlyList<ISession>> ListSessionsAsync(
        string appName,
        string? userId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a session.
    /// </summary>
    /// <param name="appName">The name of the app</param>
    /// <param name="userId">The ID of the user</param>
    /// <param name="sessionId">The session ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task DeleteSessionAsync(
        string appName,
        string userId,
        string sessionId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Appends an event to a session.
    /// Updates session state based on event actions.
    /// </summary>
    /// <param name="session">The session to append to</param>
    /// <param name="event">The event to append</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The appended event</returns>
    Task<IEvent> AppendEventAsync(
        ISession session,
        IEvent @event,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Configuration for getting a session with event filtering.
/// </summary>
public class GetSessionConfig
{
    /// <summary>
    /// Get only the N most recent events
    /// </summary>
    public int? NumRecentEvents { get; init; }

    /// <summary>
    /// Get only events after this timestamp
    /// </summary>
    public DateTimeOffset? AfterTimestamp { get; init; }
}
