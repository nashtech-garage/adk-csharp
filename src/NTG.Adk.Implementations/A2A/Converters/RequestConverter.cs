// Copyright 2025 NTG
// Licensed under Apache License, Version 2.0

using NTG.Adk.Boundary.Events;
using NTG.Adk.Implementations.A2A.Models;

namespace NTG.Adk.Implementations.A2A.Converters;

/// <summary>
/// Converts A2A messages to ADK agent run requests.
/// Based on Google ADK Python implementation.
/// </summary>
public static class RequestConverter
{
    /// <summary>
    /// Converts an A2A AgentMessage to an ADK AgentRunRequest.
    /// Extracts user ID from context or generates a default.
    /// </summary>
    /// <param name="message">The A2A message</param>
    /// <param name="contextId">The A2A context ID (format: "ADK/app/user/session")</param>
    /// <param name="userName">Optional user name from authentication context</param>
    /// <returns>ADK agent run request</returns>
    public static AgentRunRequest ConvertA2AMessageToAgentRunRequest(
        global::A2A.AgentMessage message,
        string? contextId = null,
        string? userName = null)
    {
        // Extract user ID from context or use authenticated user or generate default
        var userId = GetUserId(contextId, userName);
        var sessionId = contextId ?? Guid.NewGuid().ToString();

        // Convert A2A message parts to ADK content
        var adkParts = PartConverter.ConvertA2APartsToAdkParts(message.Parts ?? []);

        var newMessage = new Content
        {
            Role = ConvertRole(message.Role),
            Parts = adkParts
        };

        // Extract metadata if present
        var customMetadata = new Dictionary<string, object>();
        if (message.Metadata != null && message.Metadata.Count > 0)
        {
            customMetadata["a2a_metadata"] = message.Metadata;
        }

        return new AgentRunRequest
        {
            UserId = userId,
            SessionId = sessionId,
            NewMessage = newMessage,
            CustomMetadata = customMetadata
        };
    }

    /// <summary>
    /// Gets user ID from context ID, authenticated user name, or generates default.
    /// </summary>
    private static string GetUserId(string? contextId, string? userName)
    {
        // Try to extract from authenticated user
        if (!string.IsNullOrWhiteSpace(userName))
        {
            return userName;
        }

        // Try to extract from context ID
        if (!string.IsNullOrWhiteSpace(contextId))
        {
            var (_, userId, _) = MetadataUtilities.FromA2AContextId(contextId);
            if (!string.IsNullOrWhiteSpace(userId))
            {
                return userId;
            }
        }

        // Generate default user ID from context
        return $"A2A_USER_{contextId ?? Guid.NewGuid().ToString()}";
    }

    /// <summary>
    /// Converts A2A MessageRole to ADK role string.
    /// </summary>
    private static string? ConvertRole(global::A2A.MessageRole role)
    {
        return role switch
        {
            global::A2A.MessageRole.User => "user",
            global::A2A.MessageRole.Agent => "model",
            _ => null
        };
    }
}
