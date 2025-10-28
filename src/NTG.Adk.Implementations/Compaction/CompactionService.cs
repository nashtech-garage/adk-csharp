// Copyright 2025 NTG
// Licensed under the Apache License, Version 2.0

using NTG.Adk.CoreAbstractions.Compaction;
using NTG.Adk.CoreAbstractions.Events;
using NTG.Adk.CoreAbstractions.Sessions;

namespace NTG.Adk.Implementations.Compaction;

/// <summary>
/// Sliding window compaction service.
/// Implements the Python ADK compaction algorithm from google.adk.apps.compaction
/// A.D.D V3: Implementations layer (adapter)
/// </summary>
public class CompactionService
{
    /// <summary>
    /// Run compaction for sliding window if needed.
    /// Based on Python _run_compaction_for_sliding_window function.
    /// </summary>
    public static async Task RunCompactionIfNeededAsync(
        ISession session,
        ISessionService sessionService,
        EventsCompactionConfig config,
        CancellationToken cancellationToken = default)
    {
        if (config == null || !config.Enabled)
            return;

        var events = session.Events.ToList();
        if (events.Count == 0)
            return;

        // Find last compaction event's end timestamp
        double lastCompactedEndTimestamp = 0.0;
        for (int i = events.Count - 1; i >= 0; i--)
        {
            var evt = events[i];
            if (evt.Actions?.Compaction?.EndTimestamp != null)
            {
                lastCompactedEndTimestamp = evt.Actions.Compaction.EndTimestamp;
                break;
            }
        }

        // Get unique invocation IDs and their latest timestamps
        var invocationLatestTimestamps = new Dictionary<string, double>();
        foreach (var evt in events)
        {
            // Only consider non-compaction events
            if (!string.IsNullOrEmpty(evt.InvocationId) && evt.Actions?.Compaction == null)
            {
                var timestamp = evt.Timestamp.ToUnixTimeSeconds();
                if (!invocationLatestTimestamps.ContainsKey(evt.InvocationId))
                {
                    invocationLatestTimestamps[evt.InvocationId] = timestamp;
                }
                else
                {
                    invocationLatestTimestamps[evt.InvocationId] = Math.Max(
                        invocationLatestTimestamps[evt.InvocationId],
                        timestamp);
                }
            }
        }

        var uniqueInvocationIds = invocationLatestTimestamps.Keys.ToList();

        // Determine new invocations since last compaction
        var newInvocationIds = uniqueInvocationIds
            .Where(invId => invocationLatestTimestamps[invId] > lastCompactedEndTimestamp)
            .ToList();

        if (newInvocationIds.Count < config.CompactionInterval)
        {
            return; // Not enough new invocations
        }

        // Determine range of invocations to compact
        var endInvId = newInvocationIds[newInvocationIds.Count - 1];
        var firstNewInvId = newInvocationIds[0];
        var firstNewInvIdx = uniqueInvocationIds.IndexOf(firstNewInvId);

        var startIdx = Math.Max(0, firstNewInvIdx - config.OverlapSize);
        var startInvId = uniqueInvocationIds[startIdx];

        // Find last event with endInvId
        int lastEventIdx = -1;
        for (int i = events.Count - 1; i >= 0; i--)
        {
            if (events[i].InvocationId == endInvId)
            {
                lastEventIdx = i;
                break;
            }
        }

        if (lastEventIdx == -1)
            return;

        // Find first event with startInvId
        int firstEventStartInvIdx = -1;
        for (int i = 0; i < events.Count; i++)
        {
            if (events[i].InvocationId == startInvId)
            {
                firstEventStartInvIdx = i;
                break;
            }
        }

        if (firstEventStartInvIdx == -1)
            return;

        // Extract events to compact
        var eventsToCompact = events
            .Skip(firstEventStartInvIdx)
            .Take(lastEventIdx - firstEventStartInvIdx + 1)
            .Where(e => e.Actions?.Compaction == null) // Filter out existing compaction events
            .ToList();

        if (eventsToCompact.Count == 0)
            return;

        // Create or get summarizer
        if (config.Summarizer == null)
        {
            throw new InvalidOperationException(
                "EventsCompactionConfig.Summarizer must be set before running compaction");
        }

        // Generate compaction summary
        var compactionEvent = await config.Summarizer.MaybeSummarizeEventsAsync(
            eventsToCompact,
            cancellationToken);

        if (compactionEvent != null)
        {
            await sessionService.AppendEventAsync(session, compactionEvent, cancellationToken);
        }
    }
}
