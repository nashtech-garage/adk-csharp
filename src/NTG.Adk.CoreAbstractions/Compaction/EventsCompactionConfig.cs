// Copyright 2025 NTG
// Licensed under the Apache License, Version 2.0

namespace NTG.Adk.CoreAbstractions.Compaction;

/// <summary>
/// Configuration for sliding window compaction.
/// Equivalent to EventsCompactionConfig in Python ADK.
/// </summary>
public class EventsCompactionConfig
{
    /// <summary>
    /// Number of new invocations before triggering compaction.
    /// (Called compaction_invocation_threshold in Python ADK)
    /// </summary>
    public int CompactionInterval { get; set; } = 10;

    /// <summary>
    /// Number of invocations to overlap from previous compacted range.
    /// Creates context continuity between compacted summaries.
    /// </summary>
    public int OverlapSize { get; set; } = 2;

    /// <summary>
    /// Event summarizer for generating compaction summaries.
    /// If null, a default LlmEventSummarizer will be created.
    /// </summary>
    public IEventSummarizer? Summarizer { get; set; }

    /// <summary>
    /// Enable or disable compaction
    /// </summary>
    public bool Enabled { get; set; } = true;
}
