// Copyright 2025 NTG
// Licensed under Apache License, Version 2.0

using System.Text.Json;
using NTG.Adk.CoreAbstractions.Events;
using NTG.Adk.Boundary.Events;

namespace NTG.Adk.Implementations.A2A.Converters;

/// <summary>
/// Converts ADK events to A2A events for task updates.
/// Based on Google ADK Python implementation.
/// </summary>
public static class EventConverter
{
    /// <summary>
    /// Converts an ADK Event to A2A task events.
    /// May return multiple events for a single ADK event.
    /// </summary>
    /// <param name="adkEvent">The ADK event to convert</param>
    /// <param name="taskId">The A2A task ID</param>
    /// <param name="contextId">The A2A context ID</param>
    /// <param name="appName">Application name for metadata</param>
    /// <param name="userId">User ID for metadata</param>
    /// <param name="sessionId">Session ID for metadata</param>
    /// <returns>List of A2A events</returns>
    public static List<global::A2A.A2AEvent> ConvertAdkEventToA2AEvents(
        IEvent adkEvent,
        string taskId,
        string? contextId,
        string? appName = null,
        string? userId = null,
        string? sessionId = null)
    {
        var events = new List<global::A2A.A2AEvent>();

        // Create metadata with ADK context information
        var metadata = CreateMetadata(appName, userId, sessionId);

        // If event has content, create artifact update event
        if (adkEvent.Content?.Parts != null && adkEvent.Content.Parts.Count > 0)
        {
            // Convert IPart to Part first
            var adkParts = ConvertIPartsToParts(adkEvent.Content.Parts);
            var a2aParts = PartConverter.ConvertAdkPartsToA2AParts(adkParts);

            if (a2aParts.Count > 0)
            {
                // Message created for documentation purposes but not used currently
                // Can be used for more detailed event tracking in the future

                // Create artifact update event
                var artifactEvent = new global::A2A.TaskArtifactUpdateEvent
                {
                    TaskId = taskId,
                    ContextId = contextId ?? string.Empty,
                    Artifact = new global::A2A.Artifact
                    {
                        ArtifactId = Guid.NewGuid().ToString(),
                        Parts = a2aParts
                    },
                    LastChunk = !adkEvent.Partial
                };

                events.Add(artifactEvent);
            }
        }

        // Create status update event
        var statusEvent = new global::A2A.TaskStatusUpdateEvent
        {
            TaskId = taskId,
            ContextId = contextId ?? string.Empty,
            Status = new global::A2A.AgentTaskStatus
            {
                State = DetermineTaskState(adkEvent),
                Timestamp = adkEvent.Timestamp,
                Message = null
            },
            Final = !adkEvent.Partial && adkEvent.Actions == null,
            Metadata = metadata
        };

        events.Add(statusEvent);

        return events;
    }

    /// <summary>
    /// Creates metadata dictionary with ADK context information.
    /// </summary>
    private static Dictionary<string, JsonElement>? CreateMetadata(
        string? appName,
        string? userId,
        string? sessionId)
    {
        if (appName == null && userId == null && sessionId == null)
        {
            return null;
        }

        var metadata = new Dictionary<string, JsonElement>();

        if (appName != null)
        {
            metadata[MetadataUtilities.GetAdkMetadataKey("app_name")] =
                JsonSerializer.SerializeToElement(appName);
        }

        if (userId != null)
        {
            metadata[MetadataUtilities.GetAdkMetadataKey("user_id")] =
                JsonSerializer.SerializeToElement(userId);
        }

        if (sessionId != null)
        {
            metadata[MetadataUtilities.GetAdkMetadataKey("session_id")] =
                JsonSerializer.SerializeToElement(sessionId);
        }

        return metadata;
    }

    /// <summary>
    /// Determines A2A task state from ADK event.
    /// </summary>
    private static global::A2A.TaskState DetermineTaskState(IEvent adkEvent)
    {
        // Check for errors
        if (adkEvent.Metadata?.ContainsKey("error") == true)
        {
            return global::A2A.TaskState.Failed;
        }

        // Check if completed (non-partial with content)
        if (!adkEvent.Partial && adkEvent.Content != null)
        {
            return global::A2A.TaskState.Completed;
        }

        // Still working (partial or no content yet)
        return global::A2A.TaskState.Working;
    }

    /// <summary>
    /// Converts ADK role string to A2A MessageRole enum.
    /// </summary>
    private static global::A2A.MessageRole ConvertRoleToA2A(string? role)
    {
        return role?.ToLowerInvariant() switch
        {
            "user" => global::A2A.MessageRole.User,
            "model" => global::A2A.MessageRole.Agent,
            "agent" => global::A2A.MessageRole.Agent,
            _ => global::A2A.MessageRole.Agent
        };
    }

    /// <summary>
    /// Converts IPart list to Part list (CoreAbstractions → Boundary).
    /// </summary>
    private static List<Part> ConvertIPartsToParts(IReadOnlyList<IPart> iParts)
    {
        var parts = new List<Part>();
        foreach (var iPart in iParts)
        {
            // Convert based on content type
            if (iPart.Text != null)
            {
                parts.Add(Part.FromText(iPart.Text));
            }
            else if (iPart.FunctionCall != null)
            {
                var functionCall = new FunctionCall
                {
                    Name = iPart.FunctionCall.Name,
                    Args = iPart.FunctionCall.Args != null
                        ? new Dictionary<string, object>(iPart.FunctionCall.Args)
                        : null,
                    Id = iPart.FunctionCall.Id
                };
                parts.Add(Part.FromFunctionCall(functionCall));
            }
            else if (iPart.FunctionResponse != null)
            {
                var functionResponse = new FunctionResponse
                {
                    Name = iPart.FunctionResponse.Name,
                    Response = iPart.FunctionResponse.Response,
                    Id = iPart.FunctionResponse.Id,
                    Error = iPart.FunctionResponse.Error
                };
                parts.Add(Part.FromFunctionResponse(functionResponse));
            }
            else if (iPart.InlineData != null && iPart.MimeType != null)
            {
                parts.Add(Part.FromBytes(iPart.InlineData, iPart.MimeType));
            }
        }
        return parts;
    }
}
