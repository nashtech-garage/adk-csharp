// Copyright 2025 NTG
// Licensed under Apache License, Version 2.0

namespace NTG.Adk.Implementations.A2A.Converters;

/// <summary>
/// Utilities for A2A metadata and context ID management.
/// Based on Google ADK Python implementation.
/// </summary>
public static class MetadataUtilities
{
    public const string AdkMetadataKeyPrefix = "adk_";
    public const string AdkContextIdPrefix = "ADK";
    public const string AdkContextIdSeparator = "/";

    /// <summary>
    /// Gets the A2A event metadata key for the given key by prefixing it with "adk_".
    /// </summary>
    /// <param name="key">The metadata key to prefix.</param>
    /// <returns>The prefixed metadata key.</returns>
    /// <exception cref="ArgumentException">If key is null or empty.</exception>
    public static string GetAdkMetadataKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ArgumentException("Metadata key cannot be null or empty", nameof(key));
        }

        return $"{AdkMetadataKeyPrefix}{key}";
    }

    /// <summary>
    /// Converts app name, user ID, and session ID to an A2A context ID.
    /// Format: "ADK/app_name/user_id/session_id"
    /// </summary>
    /// <param name="appName">The app name.</param>
    /// <param name="userId">The user ID.</param>
    /// <param name="sessionId">The session ID.</param>
    /// <returns>The A2A context ID.</returns>
    /// <exception cref="ArgumentException">If any parameter is null or empty.</exception>
    public static string ToA2AContextId(string appName, string userId, string sessionId)
    {
        if (string.IsNullOrWhiteSpace(appName))
        {
            throw new ArgumentException("App name cannot be null or empty", nameof(appName));
        }
        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new ArgumentException("User ID cannot be null or empty", nameof(userId));
        }
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            throw new ArgumentException("Session ID cannot be null or empty", nameof(sessionId));
        }

        return string.Join(AdkContextIdSeparator, AdkContextIdPrefix, appName, userId, sessionId);
    }

    /// <summary>
    /// Converts an A2A context ID to app name, user ID, and session ID.
    /// Returns (null, null, null) if context ID is invalid or not in expected format.
    /// </summary>
    /// <param name="contextId">The A2A context ID.</param>
    /// <returns>Tuple of (appName, userId, sessionId) or (null, null, null) if invalid.</returns>
    public static (string? AppName, string? UserId, string? SessionId) FromA2AContextId(string? contextId)
    {
        if (string.IsNullOrWhiteSpace(contextId))
        {
            return (null, null, null);
        }

        try
        {
            var parts = contextId.Split(AdkContextIdSeparator);
            if (parts.Length != 4)
            {
                return (null, null, null);
            }

            var prefix = parts[0];
            var appName = parts[1];
            var userId = parts[2];
            var sessionId = parts[3];

            if (prefix == AdkContextIdPrefix &&
                !string.IsNullOrWhiteSpace(appName) &&
                !string.IsNullOrWhiteSpace(userId) &&
                !string.IsNullOrWhiteSpace(sessionId))
            {
                return (appName, userId, sessionId);
            }
        }
        catch
        {
            // Handle any parsing errors gracefully
        }

        return (null, null, null);
    }
}
