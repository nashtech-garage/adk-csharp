// Copyright 2025 NTG
// Licensed under the Apache License, Version 2.0

using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using NTG.Adk.CoreAbstractions.Events;
using NTG.Adk.CoreAbstractions.Memory;
using NTG.Adk.CoreAbstractions.Sessions;

namespace NTG.Adk.Implementations.Memory;

/// <summary>
/// In-memory implementation of searchable conversation memory.
/// Equivalent to google.adk.memory.InMemoryMemoryService in Python.
///
/// Stores conversation sessions and enables keyword-based search
/// across past conversations.
///
/// Note: Not suitable for production - data is lost when process terminates.
/// Use for testing and development only.
/// </summary>
public class InMemoryMemoryService : IMemoryService
{
    // Storage: appName/userId -> sessionId -> events
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, List<StoredEvent>>> _sessionEvents = new();

    public Task AddSessionToMemoryAsync(
        ISession session,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);

        var userKey = $"{session.AppName}/{session.UserId}";
        var userSessions = _sessionEvents.GetOrAdd(userKey, _ => new ConcurrentDictionary<string, List<StoredEvent>>());

        // Extract events with content
        var events = session.Events
            .Where(e => e.Content?.Parts != null && e.Content.Parts.Any())
            .Select(e => new StoredEvent
            {
                Content = e.Content!,
                Author = e.Author,
                Timestamp = DateTime.UtcNow.ToString("o")
            })
            .ToList();

        userSessions[session.SessionId] = events;

        return Task.CompletedTask;
    }

    public Task<ISearchMemoryResponse> SearchMemoryAsync(
        string appName,
        string userId,
        string query,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(appName))
            throw new ArgumentException("App name cannot be empty", nameof(appName));
        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException("User ID cannot be empty", nameof(userId));
        if (string.IsNullOrWhiteSpace(query))
            throw new ArgumentException("Query cannot be empty", nameof(query));

        var userKey = $"{appName}/{userId}";
        var memories = new List<IMemoryEntry>();

        if (!_sessionEvents.TryGetValue(userKey, out var userSessions))
        {
            return Task.FromResult<ISearchMemoryResponse>(new SearchMemoryResponseAdapter(memories));
        }

        // Extract search keywords
        var queryWords = ExtractWords(query.ToLowerInvariant());

        // Search all sessions for matching events
        foreach (var sessionEvents in userSessions.Values)
        {
            foreach (var evt in sessionEvents)
            {
                var eventText = ExtractTextFromContent(evt.Content).ToLowerInvariant();
                var eventWords = ExtractWords(eventText);

                // Match if any query word appears in event
                if (queryWords.Any(qw => eventWords.Contains(qw)))
                {
                    memories.Add(new MemoryEntryAdapter(evt));
                }
            }
        }

        return Task.FromResult<ISearchMemoryResponse>(new SearchMemoryResponseAdapter(memories));
    }

    private static HashSet<string> ExtractWords(string text)
    {
        // Extract words (alphanumeric sequences of 3+ chars)
        var words = Regex.Matches(text, @"\b\w{3,}\b")
            .Select(m => m.Value.ToLowerInvariant())
            .ToHashSet();
        return words;
    }

    private static string ExtractTextFromContent(IContent content)
    {
        var parts = new List<string>();
        foreach (var part in content.Parts)
        {
            if (part.Text != null)
            {
                parts.Add(part.Text);
            }
        }
        return string.Join(" ", parts);
    }

    // Internal storage for events
    private sealed class StoredEvent
    {
        public required IContent Content { get; init; }
        public string? Author { get; init; }
        public string? Timestamp { get; init; }
    }

    // Adapter from StoredEvent to IMemoryEntry
    private sealed class MemoryEntryAdapter : IMemoryEntry
    {
        private readonly StoredEvent _event;

        public MemoryEntryAdapter(StoredEvent evt)
        {
            _event = evt;
        }

        public IContent Content => _event.Content;
        public string? Author => _event.Author;
        public string? Timestamp => _event.Timestamp;
    }

    // Adapter for search response
    private sealed class SearchMemoryResponseAdapter : ISearchMemoryResponse
    {
        public IReadOnlyList<IMemoryEntry> Memories { get; }

        public SearchMemoryResponseAdapter(List<IMemoryEntry> memories)
        {
            Memories = memories;
        }
    }
}
