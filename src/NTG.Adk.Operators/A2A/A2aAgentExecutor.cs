// Copyright 2025 NTG
// Licensed under Apache License, Version 2.0

using NTG.Adk.Implementations.A2A.Converters;
using NTG.Adk.Operators.Runners;

namespace NTG.Adk.Operators.A2A;

/// <summary>
/// Bridges ADK Runner to A2A TaskManager.
/// Handles A2A callbacks and converts between A2A and ADK types.
/// Based on Google ADK Python implementation.
/// </summary>
public sealed class A2aAgentExecutor
{
    private readonly Runner _runner;
    private readonly string _appName;

    /// <summary>
    /// Creates a new A2A agent executor.
    /// </summary>
    /// <param name="runner">The ADK runner to execute</param>
    /// <param name="appName">Optional application name (defaults to runner's app name)</param>
    public A2aAgentExecutor(Runner runner, string? appName = null)
    {
        _runner = runner ?? throw new ArgumentNullException(nameof(runner));
        _appName = appName ?? runner.AppName;
    }

    /// <summary>
    /// Handles incoming A2A message and executes the agent.
    /// This is called by A2A TaskManager's OnMessageReceived callback.
    /// </summary>
    /// <param name="message">The A2A message from user</param>
    /// <param name="taskId">The A2A task ID</param>
    /// <param name="contextId">The A2A context ID</param>
    /// <param name="userName">Optional authenticated user name</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Async enumerable of A2A events</returns>
    public async IAsyncEnumerable<global::A2A.A2AEvent> ExecuteAsync(
        global::A2A.AgentMessage message,
        string taskId,
        string? contextId = null,
        string? userName = null,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // Convert A2A message to ADK request
        var runRequest = RequestConverter.ConvertA2AMessageToAgentRunRequest(
            message,
            contextId,
            userName);

        // Emit task submitted event
        yield return new global::A2A.TaskStatusUpdateEvent
        {
            TaskId = taskId,
            ContextId = contextId ?? string.Empty,
            Status = new global::A2A.AgentTaskStatus
            {
                State = global::A2A.TaskState.Submitted,
                Timestamp = DateTimeOffset.UtcNow,
                Message = message
            },
            Final = false,
            Metadata = CreateContextMetadata(runRequest.UserId, runRequest.SessionId)
        };

        // Emit task working event with metadata
        yield return new global::A2A.TaskStatusUpdateEvent
        {
            TaskId = taskId,
            ContextId = contextId ?? string.Empty,
            Status = new global::A2A.AgentTaskStatus
            {
                State = global::A2A.TaskState.Working,
                Timestamp = DateTimeOffset.UtcNow
            },
            Final = false,
            Metadata = CreateContextMetadata(runRequest.UserId, runRequest.SessionId)
        };

        // Run the ADK agent and stream events
        await foreach (var adkEvent in _runner.RunAsync(
            runRequest.UserId,
            runRequest.SessionId,
            runRequest.NewMessage?.Parts.FirstOrDefault()?.Text,
            runRequest.StateDelta,
            cancellationToken))
        {
            // Convert each ADK event to A2A events
            var a2aEvents = EventConverter.ConvertAdkEventToA2AEvents(
                adkEvent,
                taskId,
                contextId,
                _appName,
                runRequest.UserId,
                runRequest.SessionId);

            foreach (var a2aEvent in a2aEvents)
            {
                yield return a2aEvent;
            }
        }

        // Emit final completed event if not already sent
        yield return new global::A2A.TaskStatusUpdateEvent
        {
            TaskId = taskId,
            ContextId = contextId ?? string.Empty,
            Status = new global::A2A.AgentTaskStatus
            {
                State = global::A2A.TaskState.Completed,
                Timestamp = DateTimeOffset.UtcNow
            },
            Final = true,
            Metadata = CreateContextMetadata(runRequest.UserId, runRequest.SessionId)
        };
    }

    /// <summary>
    /// Creates metadata dictionary with ADK context information.
    /// </summary>
    private Dictionary<string, System.Text.Json.JsonElement>? CreateContextMetadata(
        string userId,
        string sessionId)
    {
        var metadata = new Dictionary<string, System.Text.Json.JsonElement>
        {
            [MetadataUtilities.GetAdkMetadataKey("app_name")] =
                System.Text.Json.JsonSerializer.SerializeToElement(_appName),
            [MetadataUtilities.GetAdkMetadataKey("user_id")] =
                System.Text.Json.JsonSerializer.SerializeToElement(userId),
            [MetadataUtilities.GetAdkMetadataKey("session_id")] =
                System.Text.Json.JsonSerializer.SerializeToElement(sessionId)
        };

        return metadata;
    }
}
