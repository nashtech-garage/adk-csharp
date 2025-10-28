// Copyright 2025 NTG
// Licensed under the Apache License, Version 2.0

using NTG.Adk.CoreAbstractions.Events;
using NTG.Adk.CoreAbstractions.Sessions;

namespace NTG.Adk.Implementations.Sessions;

/// <summary>
/// In-memory implementation of IMessageHistory.
/// Builds messages from session events on-the-fly (Python ADK approach).
/// </summary>
public class InMemoryMessageHistory : IMessageHistory
{
    private readonly List<IEvent> _events;

    public InMemoryMessageHistory(List<IEvent> events)
    {
        _events = events ?? throw new ArgumentNullException(nameof(events));
    }

    /// <summary>
    /// Get all messages by filtering events with user/model content
    /// </summary>
    public IReadOnlyList<IMessage> GetAll()
    {
        var messages = new List<IMessage>();

        foreach (var evt in _events)
        {
            // Skip events without content
            if (evt.Content == null || evt.Content.Parts == null || evt.Content.Parts.Count == 0)
                continue;

            // Skip non-user, non-model events
            var role = evt.Content.Role;
            if (role != "user" && role != "model")
                continue;

            // Convert event to message
            messages.Add(new InMemoryEventMessage(evt));
        }

        return messages;
    }

    /// <summary>
    /// Get messages for a specific branch
    /// </summary>
    public IReadOnlyList<IMessage> GetBranch(string branch)
    {
        var messages = new List<IMessage>();

        foreach (var evt in _events)
        {
            // Filter by branch
            if (evt.Branch != branch)
                continue;

            // Skip events without content
            if (evt.Content == null || evt.Content.Parts == null || evt.Content.Parts.Count == 0)
                continue;

            // Skip non-user, non-model events
            var role = evt.Content.Role;
            if (role != "user" && role != "model")
                continue;

            // Convert event to message
            messages.Add(new InMemoryEventMessage(evt));
        }

        return messages;
    }

    /// <summary>
    /// Add a message (deprecated - use events directly)
    /// </summary>
    public void Add(IMessage message)
    {
        // For backward compatibility only
        // In practice, events should be added directly to session.Events
        throw new NotSupportedException(
            "Add() is deprecated. Messages are built from events. " +
            "Add events to session.Events instead.");
    }

    /// <summary>
    /// Clear history (deprecated - use compaction instead)
    /// For compatibility, removes all user/model content events
    /// </summary>
    public void Clear()
    {
        // Remove events with user/model content
        _events.RemoveAll(evt =>
        {
            if (evt.Content == null) return false;
            var role = evt.Content.Role;
            return role == "user" || role == "model";
        });
    }
}

/// <summary>
/// Message implementation built from event
/// </summary>
internal class InMemoryEventMessage : IMessage
{
    public string? Role { get; }
    public string? Content { get; }
    public string? Branch { get; }
    public DateTimeOffset Timestamp { get; }

    public InMemoryEventMessage(IEvent evt)
    {
        Role = evt.Content?.Role;
        Branch = evt.Branch;
        Timestamp = evt.Timestamp;

        // Extract text content from parts
        if (evt.Content?.Parts != null)
        {
            var textParts = evt.Content.Parts
                .Where(p => !string.IsNullOrEmpty(p.Text))
                .Select(p => p.Text);
            Content = string.Join("", textParts);
        }
    }
}
