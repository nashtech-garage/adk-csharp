// Copyright 2025 NTG
// Licensed under the Apache License, Version 2.0

using System.Data;
using System.Data.Common;
using System.Text.Json;
using Dapper;
using Microsoft.Data.Sqlite;
using MySqlConnector;
using Npgsql;
using NTG.Adk.Boundary.Events;
using NTG.Adk.CoreAbstractions.Events;
using NTG.Adk.CoreAbstractions.Sessions;
using NTG.Adk.Implementations.Events;

namespace NTG.Adk.Implementations.Sessions;

/// <summary>
/// Database-backed implementation of ISessionService.
/// Supports SQLite, PostgreSQL, and MySQL.
/// Suitable for production multi-user environments with persistence.
/// </summary>
public class DatabaseSessionService : ISessionService
{
    private readonly string _connectionString;
    private readonly DatabaseType _databaseType;
    private readonly Func<DbConnection> _connectionFactory;

    public DatabaseSessionService(string connectionString)
    {
        _connectionString = connectionString;
        _databaseType = DetectDatabaseType(connectionString);
        _connectionFactory = CreateConnectionFactory();

        // Initialize schema
        InitializeSchemaAsync().Wait();
    }

    public async Task<ISession> CreateSessionAsync(
        string appName,
        string userId,
        IReadOnlyDictionary<string, object>? state = null,
        string? sessionId = null,
        CancellationToken cancellationToken = default)
    {
        sessionId = string.IsNullOrWhiteSpace(sessionId) ? Guid.NewGuid().ToString() : sessionId.Trim();

        using var connection = _connectionFactory();
        await connection.OpenAsync(cancellationToken);

        // Check if session exists
        var existing = await GetSessionAsync(appName, userId, sessionId, cancellationToken: cancellationToken);
        if (existing != null)
        {
            throw new InvalidOperationException($"Session '{sessionId}' already exists");
        }

        var now = DateTimeOffset.UtcNow;

        // Insert session
        await connection.ExecuteAsync(
            "INSERT INTO sessions (app_name, user_id, session_id, last_update_time, created_at) VALUES (@AppName, @UserId, @SessionId, @LastUpdateTime, @CreatedAt)",
            new { AppName = appName, UserId = userId, SessionId = sessionId, LastUpdateTime = now, CreatedAt = now });

        // Insert state if provided
        if (state != null)
        {
            foreach (var kvp in state)
            {
                await connection.ExecuteAsync(
                    "INSERT INTO session_state (app_name, user_id, session_id, state_key, state_value) VALUES (@AppName, @UserId, @SessionId, @Key, @Value)",
                    new { AppName = appName, UserId = userId, SessionId = sessionId, Key = kvp.Key, Value = JsonSerializer.Serialize(kvp.Value) });
            }
        }

        return await GetSessionAsync(appName, userId, sessionId, cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("Failed to create session");
    }

    public async Task<ISession?> GetSessionAsync(
        string appName,
        string userId,
        string sessionId,
        GetSessionConfig? config = null,
        CancellationToken cancellationToken = default)
    {
        using var connection = _connectionFactory();
        await connection.OpenAsync(cancellationToken);

        // Get session record
        var sessionRow = await connection.QuerySingleOrDefaultAsync<SessionRow>(
            "SELECT app_name, user_id, session_id, last_update_time, created_at FROM sessions WHERE app_name = @AppName AND user_id = @UserId AND session_id = @SessionId",
            new { AppName = appName, UserId = userId, SessionId = sessionId });

        if (sessionRow == null)
            return null;

        // Load state
        var stateRows = await connection.QueryAsync<StateRow>(
            "SELECT state_key, state_value FROM session_state WHERE app_name = @AppName AND user_id = @UserId AND session_id = @SessionId",
            new { AppName = appName, UserId = userId, SessionId = sessionId });

        var sessionState = new InMemorySessionState();
        foreach (var row in stateRows)
        {
            var value = JsonSerializer.Deserialize<object>(row.StateValue);
            if (value != null)
            {
                sessionState.Set(row.StateKey, value);
            }
        }

        // Load events
        var eventRows = await connection.QueryAsync<EventRow>(
            "SELECT event_data FROM session_events WHERE app_name = @AppName AND user_id = @UserId AND session_id = @SessionId ORDER BY event_index",
            new { AppName = appName, UserId = userId, SessionId = sessionId });

        var events = new List<IEvent>();
        foreach (var row in eventRows)
        {
            var evt = JsonSerializer.Deserialize<Event>(row.EventData);
            if (evt != null)
            {
                events.Add(new EventAdapter(evt));
            }
        }

        // Apply config filters
        if (config != null)
        {
            if (config.NumRecentEvents.HasValue)
            {
                events = events.Skip(Math.Max(0, events.Count - config.NumRecentEvents.Value)).ToList();
            }

            if (config.AfterTimestamp.HasValue)
            {
                events = events.Where(e => e.Timestamp >= config.AfterTimestamp.Value).ToList();
            }
        }

        return new InMemorySession(
            appName: sessionRow.AppName,
            userId: sessionRow.UserId,
            sessionId: sessionRow.SessionId,
            state: sessionState,
            events: events,
            lastUpdateTime: sessionRow.LastUpdateTime);
    }

    public async Task<IReadOnlyList<ISession>> ListSessionsAsync(
        string appName,
        string? userId = null,
        CancellationToken cancellationToken = default)
    {
        using var connection = _connectionFactory();
        await connection.OpenAsync(cancellationToken);

        IEnumerable<SessionRow> sessionRows;
        if (userId != null)
        {
            sessionRows = await connection.QueryAsync<SessionRow>(
                "SELECT app_name, user_id, session_id, last_update_time, created_at FROM sessions WHERE app_name = @AppName AND user_id = @UserId",
                new { AppName = appName, UserId = userId });
        }
        else
        {
            sessionRows = await connection.QueryAsync<SessionRow>(
                "SELECT app_name, user_id, session_id, last_update_time, created_at FROM sessions WHERE app_name = @AppName",
                new { AppName = appName });
        }

        var sessions = new List<ISession>();
        foreach (var row in sessionRows)
        {
            var session = new InMemorySession(
                appName: row.AppName,
                userId: row.UserId,
                sessionId: row.SessionId,
                state: new InMemorySessionState(),
                events: new List<IEvent>(),
                lastUpdateTime: row.LastUpdateTime);
            sessions.Add(session);
        }

        return sessions;
    }

    public async Task DeleteSessionAsync(
        string appName,
        string userId,
        string sessionId,
        CancellationToken cancellationToken = default)
    {
        using var connection = _connectionFactory();
        await connection.OpenAsync(cancellationToken);

        await connection.ExecuteAsync(
            "DELETE FROM session_events WHERE app_name = @AppName AND user_id = @UserId AND session_id = @SessionId",
            new { AppName = appName, UserId = userId, SessionId = sessionId });

        await connection.ExecuteAsync(
            "DELETE FROM session_state WHERE app_name = @AppName AND user_id = @UserId AND session_id = @SessionId",
            new { AppName = appName, UserId = userId, SessionId = sessionId });

        await connection.ExecuteAsync(
            "DELETE FROM sessions WHERE app_name = @AppName AND user_id = @UserId AND session_id = @SessionId",
            new { AppName = appName, UserId = userId, SessionId = sessionId });
    }

    public async Task<IEvent> AppendEventAsync(
        ISession session,
        IEvent @event,
        CancellationToken cancellationToken = default)
    {
        if (@event.Partial)
            return @event;

        using var connection = _connectionFactory();
        await connection.OpenAsync(cancellationToken);

        // Get next event index
        var maxIndex = await connection.ExecuteScalarAsync<int?>(
            "SELECT MAX(event_index) FROM session_events WHERE app_name = @AppName AND user_id = @UserId AND session_id = @SessionId",
            new { AppName = session.AppName, UserId = session.UserId, SessionId = session.SessionId });

        var eventIndex = (maxIndex ?? -1) + 1;

        // Convert to DTO for serialization
        var eventDto = ConvertToEventDto(@event);

        // Insert event
        await connection.ExecuteAsync(
            "INSERT INTO session_events (app_name, user_id, session_id, event_index, event_data) VALUES (@AppName, @UserId, @SessionId, @EventIndex, @EventData)",
            new { AppName = session.AppName, UserId = session.UserId, SessionId = session.SessionId, EventIndex = eventIndex, EventData = JsonSerializer.Serialize(eventDto) });

        // Update session timestamp
        await connection.ExecuteAsync(
            "UPDATE sessions SET last_update_time = @LastUpdateTime WHERE app_name = @AppName AND user_id = @UserId AND session_id = @SessionId",
            new { AppName = session.AppName, UserId = session.UserId, SessionId = session.SessionId, LastUpdateTime = @event.Timestamp });

        // Update state deltas
        if (@event.Actions?.StateDelta != null)
        {
            foreach (var kvp in @event.Actions.StateDelta)
            {
                // Delete existing state key
                await connection.ExecuteAsync(
                    "DELETE FROM session_state WHERE app_name = @AppName AND user_id = @UserId AND session_id = @SessionId AND state_key = @Key",
                    new { AppName = session.AppName, UserId = session.UserId, SessionId = session.SessionId, Key = kvp.Key });

                // Insert new state value
                await connection.ExecuteAsync(
                    "INSERT INTO session_state (app_name, user_id, session_id, state_key, state_value) VALUES (@AppName, @UserId, @SessionId, @Key, @Value)",
                    new { AppName = session.AppName, UserId = session.UserId, SessionId = session.SessionId, Key = kvp.Key, Value = JsonSerializer.Serialize(kvp.Value) });
            }
        }

        // Update in-memory session
        session.Events.Add(@event);
        session.LastUpdateTime = @event.Timestamp;

        return @event;
    }

    private async Task InitializeSchemaAsync()
    {
        using var connection = _connectionFactory();
        await connection.OpenAsync();

        // Sessions table
        await connection.ExecuteAsync(@"
            CREATE TABLE IF NOT EXISTS sessions (
                app_name TEXT NOT NULL,
                user_id TEXT NOT NULL,
                session_id TEXT NOT NULL,
                last_update_time TEXT NOT NULL,
                created_at TEXT NOT NULL,
                PRIMARY KEY (app_name, user_id, session_id)
            )");

        // Events table
        await connection.ExecuteAsync(@"
            CREATE TABLE IF NOT EXISTS session_events (
                app_name TEXT NOT NULL,
                user_id TEXT NOT NULL,
                session_id TEXT NOT NULL,
                event_index INTEGER NOT NULL,
                event_data TEXT NOT NULL,
                PRIMARY KEY (app_name, user_id, session_id, event_index)
            )");

        // State table
        await connection.ExecuteAsync(@"
            CREATE TABLE IF NOT EXISTS session_state (
                app_name TEXT NOT NULL,
                user_id TEXT NOT NULL,
                session_id TEXT NOT NULL,
                state_key TEXT NOT NULL,
                state_value TEXT NOT NULL,
                PRIMARY KEY (app_name, user_id, session_id, state_key)
            )");
    }

    private DatabaseType DetectDatabaseType(string connectionString)
    {
        if (connectionString.Contains("Host=") || connectionString.Contains("Server=") && connectionString.Contains("Port=5432"))
            return DatabaseType.PostgreSQL;
        if (connectionString.Contains("Server=") && (connectionString.Contains("Port=3306") || connectionString.Contains("Database=")))
            return DatabaseType.MySQL;
        return DatabaseType.SQLite;
    }

    private Func<DbConnection> CreateConnectionFactory()
    {
        return _databaseType switch
        {
            DatabaseType.PostgreSQL => () => new NpgsqlConnection(_connectionString),
            DatabaseType.MySQL => () => new MySqlConnection(_connectionString),
            DatabaseType.SQLite => () => new SqliteConnection(_connectionString),
            _ => throw new NotSupportedException($"Database type {_databaseType} not supported")
        };
    }

    private enum DatabaseType
    {
        SQLite,
        PostgreSQL,
        MySQL
    }

    private class SessionRow
    {
        public string AppName { get; set; } = "";
        public string UserId { get; set; } = "";
        public string SessionId { get; set; } = "";
        public DateTimeOffset LastUpdateTime { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
    }

    private class StateRow
    {
        public string StateKey { get; set; } = "";
        public string StateValue { get; set; } = "";
    }

    private class EventRow
    {
        public string EventData { get; set; } = "";
    }

    private static Event ConvertToEventDto(IEvent @event)
    {
        var dto = new Event
        {
            Author = @event.Author,
            Timestamp = @event.Timestamp,
            Partial = @event.Partial,
            Content = @event.Content != null ? ConvertContentToDto(@event.Content) : null,
            Actions = @event.Actions != null ? ConvertActionsToDto(@event.Actions) : null,
            Metadata = @event.Metadata != null ? new Dictionary<string, object>(@event.Metadata) : null
        };
        return dto;
    }

    private static Content ConvertContentToDto(IContent content)
    {
        var dto = new Content
        {
            Role = content.Role,
            Parts = content.Parts.Select(ConvertPartToDto).ToList()
        };
        return dto;
    }

    private static Part ConvertPartToDto(IPart part)
    {
        var dto = new Part
        {
            Text = part.Text,
            InlineData = part.InlineData,
            MimeType = part.MimeType,
            FunctionCall = part.FunctionCall != null ? new Boundary.Events.FunctionCall
            {
                Name = part.FunctionCall.Name,
                Args = part.FunctionCall.Args != null ? new Dictionary<string, object>(part.FunctionCall.Args) : null,
                Id = part.FunctionCall.Id
            } : null,
            FunctionResponse = part.FunctionResponse != null ? new Boundary.Events.FunctionResponse
            {
                Name = part.FunctionResponse.Name,
                Response = part.FunctionResponse.Response,
                Id = part.FunctionResponse.Id,
                Error = part.FunctionResponse.Error
            } : null
        };
        return dto;
    }

    private static EventActions ConvertActionsToDto(IEventActions actions)
    {
        var dto = new EventActions
        {
            Escalate = actions.Escalate,
            TransferTo = actions.TransferTo,
            StateDelta = actions.StateDelta != null ? new Dictionary<string, object>(actions.StateDelta) : null,
            CustomActions = actions.CustomActions != null ? new Dictionary<string, object>(actions.CustomActions) : null
        };
        return dto;
    }
}
