// Copyright 2025 NTG
// Licensed under the Apache License, Version 2.0

using NTG.Adk.CoreAbstractions.Events;

namespace NTG.Adk.CoreAbstractions.Compaction;

/// <summary>
/// Port interface for event summarization (sliding window compaction).
/// Equivalent to BaseEventsSummarizer in Python ADK.
/// </summary>
public interface IEventSummarizer
{
    /// <summary>
    /// Summarizes a list of events into a single compacted event.
    /// </summary>
    /// <param name="events">Events to summarize</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Compacted event or null if no summarization needed</returns>
    Task<IEvent?> MaybeSummarizeEventsAsync(
        IReadOnlyList<IEvent> events,
        CancellationToken cancellationToken = default);
}
